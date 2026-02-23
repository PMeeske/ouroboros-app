// <copyright file="WebAvatarRenderer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reactive.Disposables;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ouroboros.Application.Avatar;
using Spectre.Console;

namespace Ouroboros.CLI.Avatar;

/// <summary>
/// Serves the avatar viewer HTML and relays state over WebSocket.
/// Opens the user's default browser to show the living avatar alongside the CLI.
/// </summary>
public sealed class WebAvatarRenderer : IAvatarRenderer, IVideoFrameRenderer
{
    private const int DefaultPort = 9471;
    private const int MaxPortRetries = 10;

    private int _port;
    private readonly string _assetDirectory;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;
    private readonly List<WebSocket> _clients = new();
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="WebAvatarRenderer"/> class.
    /// </summary>
    /// <param name="port">
    /// Port for the local HTTP/WebSocket server.
    /// Pass 0 to auto-assign starting from <see cref="DefaultPort"/>.
    /// </param>
    /// <param name="assetDirectory">Directory containing avatar images and viewer HTML.</param>
    public WebAvatarRenderer(int port = 0, string? assetDirectory = null)
    {
        _port = port > 0 ? port : DefaultPort;
        _assetDirectory = assetDirectory ?? ResolveDefaultAssetPath();
    }

    /// <summary>Gets the actual port the server is listening on (valid after <see cref="StartAsync"/>).</summary>
    public int ActualPort => _port;

    /// <inheritdoc/>
    public bool IsActive => _listener?.IsListening == true;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Prepare holographic assets from character sheet (if needed)
        AvatarAssetPreparer.PrepareIaretHolographics(Path.Combine(_assetDirectory, "Iaret"));

        (_listener, _port) = StartOnAvailablePort(_port);

        _serverTask = Task.Run(() => ServerLoop(_cts.Token), _cts.Token);

        // Always auto-open browser when avatar is launched
        var url = $"http://localhost:{_port}/avatar.html";
        OpenBrowser(url);

        AnsiConsole.MarkupLine($"\n[rgb(128,0,180)]  ☥ Iaret avatar viewer: {Markup.Escape(url)}[/]");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Tries to start the listener on the preferred port, scanning forward on conflict.
    /// Creates a fresh <see cref="HttpListener"/> per attempt — on Windows, a listener
    /// that throws <see cref="HttpListenerException"/> during Start() enters a partially
    /// invalid state where subsequent method calls throw <see cref="ObjectDisposedException"/>.
    /// </summary>
    private static (HttpListener Listener, int Port) StartOnAvailablePort(int preferredPort)
    {
        for (var attempt = 0; attempt < MaxPortRetries; attempt++)
        {
            var port = preferredPort + attempt;
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");

            try
            {
                listener.Start();
                if (attempt > 0)
                {
                    Console.Error.WriteLine($"  [Avatar] Port {preferredPort} busy, using {port}");
                }

                return (listener, port);
            }
            catch (HttpListenerException)
            {
                // Port in use — discard this listener and try next port
                listener.Close();
            }
        }

        // Last resort: find any free port via the OS
        var freePort = FindFreePort();
        var lastResortListener = new HttpListener();
        lastResortListener.Prefixes.Add($"http://localhost:{freePort}/");
        lastResortListener.Start();
        Console.Error.WriteLine($"  [Avatar] Using OS-assigned port {freePort}");
        return (lastResortListener, freePort);
    }

    private static int FindFreePort()
    {
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    /// <summary>
    /// Broadcasts a raw JPEG video frame to all connected WebSocket clients.
    /// The frame is prefixed with a 0x01 type marker byte so the client can
    /// distinguish binary video frames from JSON state snapshots.
    /// </summary>
    /// <param name="jpegBytes">JPEG-encoded frame bytes.</param>
    public async Task BroadcastFrameAsync(byte[] jpegBytes)
    {
        // Prefix with message type byte: 0x01 = video frame
        var message = new byte[1 + jpegBytes.Length];
        message[0] = 0x01;
        jpegBytes.CopyTo(message, 1);

        await _clientLock.WaitAsync();
        try
        {
            var dead = new List<WebSocket>();
            foreach (var ws in _clients)
            {
                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.SendAsync(
                            new ArraySegment<byte>(message),
                            WebSocketMessageType.Binary,
                            true,
                            CancellationToken.None);
                    }
                    catch
                    {
                        dead.Add(ws);
                    }
                }
                else
                {
                    dead.Add(ws);
                }
            }

            foreach (var ws in dead)
            {
                _clients.Remove(ws);
                ws.Dispose();
            }
        }
        finally
        {
            _clientLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task UpdateStateAsync(AvatarStateSnapshot state, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(state, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        await _clientLock.WaitAsync(ct);
        try
        {
            var dead = new List<WebSocket>();
            foreach (var ws in _clients)
            {
                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.SendAsync(segment, WebSocketMessageType.Text, true, ct);
                    }
                    catch
                    {
                        dead.Add(ws);
                    }
                }
                else
                {
                    dead.Add(ws);
                }
            }

            foreach (var ws in dead)
            {
                _clients.Remove(ws);
                ws.Dispose();
            }
        }
        finally
        {
            _clientLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();
        _listener?.Stop();

        if (_serverTask != null)
        {
            try { await _serverTask; } catch (OperationCanceledException) { }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync();
        _listener?.Close();
        _cts?.Dispose();

        await _clientLock.WaitAsync();
        try
        {
            foreach (var ws in _clients) ws.Dispose();
            _clients.Clear();
        }
        finally
        {
            _clientLock.Release();
            _clientLock.Dispose();
        }
    }

    // ── Server loop ──

    private async Task ServerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener!.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    _ = HandleWebSocket(context, ct);
                }
                else
                {
                    HandleHttpRequest(context);
                }
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  [Avatar] Server error: {ex.Message}");
            }
        }
    }

    private async Task HandleWebSocket(HttpListenerContext context, CancellationToken ct)
    {
        WebSocketContext wsContext;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(null);
        }
        catch
        {
            context.Response.StatusCode = 500;
            context.Response.Close();
            return;
        }

        var ws = wsContext.WebSocket;

        await _clientLock.WaitAsync(ct);
        try { _clients.Add(ws); }
        finally { _clientLock.Release(); }

        // Keep alive until closed
        var buffer = new byte[256];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
        finally
        {
            await _clientLock.WaitAsync(CancellationToken.None);
            try { _clients.Remove(ws); }
            finally { _clientLock.Release(); }

            if (ws.State == WebSocketState.Open)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
                catch { }
            }

            ws.Dispose();
        }
    }

    private static bool TryGetSafeFilePath(string rootDir, string relativePath, out string? safePath)
    {
        safePath = null;

        if (string.IsNullOrEmpty(relativePath))
        {
            return false;
        }

        // Basic rejection of parent directory references
        if (relativePath.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        // Reject any invalid path characters to avoid malformed paths
        if (relativePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return false;
        }

        string combinedPath;
        try
        {
            combinedPath = Path.GetFullPath(Path.Combine(rootDir, relativePath));
        }
        catch
        {
            return false;
        }

        var rootFullPath = Path.GetFullPath(rootDir);
        if (!combinedPath.StartsWith(rootFullPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(combinedPath, rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        safePath = combinedPath;
        return true;
    }

    private void HandleHttpRequest(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath?.TrimStart('/') ?? "";
        if (string.IsNullOrEmpty(path)) path = "avatar.html";

        // Serve from viewer directory first, then from avatar images directory
        var viewerDir = Path.Combine(_assetDirectory, "Viewer");

        string? filePath = null;
        if (TryGetSafeFilePath(viewerDir, path, out var candidateViewerPath) && File.Exists(candidateViewerPath))
        {
            filePath = candidateViewerPath;
        }
        else
        {
            // Try avatar images directory (for idle.png, etc.)
            var iaretDir = Path.Combine(_assetDirectory, "Iaret");
            if (TryGetSafeFilePath(iaretDir, path, out var candidateIaretPath) && File.Exists(candidateIaretPath))
            {
                filePath = candidateIaretPath;
            }
        }

        if (filePath is null)
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        context.Response.ContentType = ext switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".json" => "application/json",
            _ => "application/octet-stream",
        };

        context.Response.Headers.Add("Cache-Control", "no-cache");
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");

        try
        {
            using var fs = File.OpenRead(filePath);
            fs.CopyTo(context.Response.OutputStream);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [Avatar] File serve error: {ex.Message}");
        }

        context.Response.Close();
    }

    // ── Helpers ──

    private static string ResolveDefaultAssetPath()
    {
        // Walk up from the executing assembly to find the Assets directory
        var baseDir = AppContext.BaseDirectory;

        // In development: src/Ouroboros.CLI/bin/Debug/net10.0/
        // Assets at:       src/Ouroboros.CLI/Assets/Avatar/
        var candidates = new[]
        {
            Path.Combine(baseDir, "Assets", "Avatar"),
            Path.Combine(baseDir, "..", "..", "..", "Assets", "Avatar"),
            Path.Combine(baseDir, "..", "..", "..", "..", "Ouroboros.CLI", "Assets", "Avatar"),
        };

        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (Directory.Exists(full)) return full;
        }

        // Fallback: create in temp
        var tempPath = Path.Combine(Path.GetTempPath(), "ouroboros-avatar");
        Directory.CreateDirectory(tempPath);
        return tempPath;
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
        catch
        {
            // Browser launch is best-effort
        }
    }
}
