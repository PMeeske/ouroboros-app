// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Services.RoomPresence;

using System.Diagnostics;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

/// <summary>
/// Lightweight gesture detection using periodic camera captures analysed by a vision LLM.
///
/// Captures a frame every <see cref="IntervalSeconds"/> seconds via ffmpeg,
/// then asks the vision service to identify human gestures (wave, nod, point, beckon, etc.).
/// Fires <see cref="OnGestureDetected"/> when a gesture other than NONE is recognised.
///
/// Only active when <c>--camera</c> is enabled in room mode.
/// </summary>
public sealed class GestureDetector : IAsyncDisposable
{
    private const int IntervalSeconds = 10;
    private const string GesturePrompt =
        "Look at this image from a room camera. Describe any human gestures you see: " +
        "waving, pointing, nodding, shaking head, thumbs up, beckoning, or other deliberate gestures. " +
        "Reply with the gesture type in UPPERCASE first (e.g. WAVE, NOD, POINT, BECKON, THUMBS_UP), " +
        "then a brief description. If no gestures are visible, reply NONE.";

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _disposed;
    private readonly string _captureDir;

    /// <summary>
    /// Fired when a gesture is detected.
    /// Parameters: (gestureType, description).
    /// </summary>
    public event Action<string, string>? OnGestureDetected;

    public GestureDetector()
    {
        _captureDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ouroboros", "captures");
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loop = Task.Run(() => DetectionLoopAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task DetectionLoopAsync(CancellationToken ct)
    {
        // Wait a few seconds before first capture to let room mode fully initialise
        await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var imagePath = await CaptureFrameAsync(ct).ConfigureAwait(false);
                if (imagePath != null)
                {
                    var result = await AnalyzeGestureAsync(imagePath, ct).ConfigureAwait(false);
                    if (result != null)
                    {
                        var (gestureType, description) = result.Value;
                        if (!gestureType.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                        {
                            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [gesture] Detected: {Markup.Escape(gestureType)} — {Markup.Escape(description)}"));
                            OnGestureDetected?.Invoke(gestureType, description);
                        }
                    }

                    // Clean up the capture file
                    try { File.Delete(imagePath); } catch (IOException) { /* best effort cleanup */ }
                }

                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [gesture] Error: {Markup.Escape(ex.Message)}"));
                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds * 2), ct).ConfigureAwait(false);
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [gesture] Error: {Markup.Escape(ex.Message)}"));
                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds * 2), ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Captures a single frame from the webcam via ffmpeg.
    /// Returns the image path or null if capture failed.
    /// </summary>
    private async Task<string?> CaptureFrameAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_captureDir);
        var filepath = Path.Combine(_captureDir, $"gesture_{DateTime.Now:HHmmss}.jpg");

        var cameraNames = new[] { "Integrated Camera", "Integrated Webcam", "USB Camera", "HD Webcam", "Webcam" };

        foreach (var camName in cameraNames)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("dshow");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add($"video={camName}");
            psi.ArgumentList.Add("-frames:v");
            psi.ArgumentList.Add("1");
            psi.ArgumentList.Add("-y");
            psi.ArgumentList.Add(filepath);

            try
            {
                // SECURITY: safe — hardcoded "ffmpeg" with ArgumentList for camera capture
                using var process = Process.Start(psi);
                if (process == null) continue;

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

                if (process.ExitCode == 0 && File.Exists(filepath))
                    return filepath;
            }
            catch (InvalidOperationException) { /* try next camera name */ }
            catch (System.ComponentModel.Win32Exception) { /* try next camera name */ }
        }

        return null;
    }

    /// <summary>
    /// Sends a captured frame to the local vision model for gesture analysis.
    /// Uses Ollama with a vision model via a simple prompt.
    /// Returns (gestureType, description) or null.
    /// </summary>
    private async Task<(string GestureType, string Description)?> AnalyzeGestureAsync(
        string imagePath, CancellationToken ct)
    {
        // Use Ollama vision model for image analysis
        // Use ArgumentList to prevent command injection via prompt or image path
        var psi = new ProcessStartInfo("ollama")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("llava");
        psi.ArgumentList.Add(GesturePrompt);
        psi.ArgumentList.Add("--images");
        psi.ArgumentList.Add(imagePath);

        try
        {
            // SECURITY: safe — hardcoded "ollama" with ArgumentList for vision analysis
            using var process = Process.Start(psi);
            if (process == null) return null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var output = await process.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(output)) return null;

            output = output.Trim();

            // Parse: first word is the gesture type
            var firstSpace = output.IndexOf(' ');
            if (firstSpace < 0)
                return (output.ToUpperInvariant(), output);

            var gestureType = output[..firstSpace].Trim().ToUpperInvariant();
            var description = output[(firstSpace + 1)..].Trim();
            return (gestureType, description);
        }
        catch
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_cts is { } cts)
        {
            await cts.CancelAsync();
            cts.Dispose();
            _cts = null;
        }

        if (_loop is { } loop)
        {
            try { await loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }
}
