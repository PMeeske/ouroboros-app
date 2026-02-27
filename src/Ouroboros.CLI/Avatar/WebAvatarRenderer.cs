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
public sealed partial class WebAvatarRenderer : IAvatarRenderer, IVideoFrameRenderer, IVisionTextRenderer
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
                    catch (WebSocketException)
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

    /// <summary>
    /// Broadcasts a streaming vision text token to all connected WebSocket clients.
    /// The text is prefixed with a 0x02 type marker byte so the client can
    /// distinguish vision text from video frames and JSON state snapshots.
    /// A 0x03 marker signals "new frame" (clears the previous text).
    /// </summary>
    public async Task BroadcastVisionTextAsync(string text, bool isNewFrame = false)
    {
        byte typeByte = isNewFrame ? (byte)0x03 : (byte)0x02;
        var textBytes = Encoding.UTF8.GetBytes(text);
        var message = new byte[1 + textBytes.Length];
        message[0] = typeByte;
        textBytes.CopyTo(message, 1);

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
                    catch (WebSocketException)
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
                    catch (WebSocketException)
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

}
