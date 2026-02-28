// <copyright file="WebAvatarRenderer.Server.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;

namespace Ouroboros.CLI.Avatar;

/// <summary>
/// Server loop, HTTP request handling, WebSocket management, and utility helpers
/// for the web avatar renderer.
/// </summary>
public sealed partial class WebAvatarRenderer
{
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
            catch (InvalidOperationException ex)
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
        catch (WebSocketException)
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
                catch (WebSocketException) { }
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
        catch (ArgumentException)
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
        catch (IOException ex)
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
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            return;

        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
        catch (InvalidOperationException)
        {
            // Browser launch is best-effort
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Browser launch is best-effort
        }
    }
}
