// <copyright file="WatchScreenTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Drawing;
using System.Text.Json;
using Ouroboros.Application.Extensions;
using Ouroboros.Core.Monads;

public static partial class PerceptionTools
{
    /// <summary>
    /// Watch screen for changes (polling-based).
    /// </summary>
    public class WatchScreenTool : ITool
    {
        public string Name => "watch_screen";
        public string Description => "Start watching the screen for changes. Input JSON: {\"duration_seconds\": 60, \"interval_ms\": 1000, \"sensitivity\": 0.1}. Reports when significant screen changes occur.";
        public string? JsonSchema => """{"type":"object","properties":{"duration_seconds":{"type":"integer"},"interval_ms":{"type":"integer"},"sensitivity":{"type":"number"}}}""";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "CS0169:Field is never used", Justification = "Used in conditional compilation (NET10_0_OR_GREATER_WINDOWS)")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "CS0414:Field is assigned but never used", Justification = "Used in conditional compilation (NET10_0_OR_GREATER_WINDOWS)")]
        private static CancellationTokenSource? _watchCts;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "CS0414:Field is assigned but never used", Justification = "Used in conditional compilation (NET10_0_OR_GREATER_WINDOWS)")]
        private static bool _isWatching = false;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
#if NET10_0_OR_GREATER_WINDOWS
            try
            {
                // Parse parameters
                int durationSeconds = 60;
                int intervalMs = 1000;
                double sensitivity = 0.1; // 10% change threshold

                if (!string.IsNullOrWhiteSpace(input))
                {
                    var args = JsonSerializer.Deserialize<JsonElement>(input);
                    if (args.TryGetProperty("duration_seconds", out var durEl))
                        durationSeconds = durEl.GetInt32();
                    if (args.TryGetProperty("interval_ms", out var intEl))
                        intervalMs = intEl.GetInt32();
                    if (args.TryGetProperty("sensitivity", out var sensEl))
                        sensitivity = sensEl.GetDouble();
                }

                if (_isWatching)
                {
                    _watchCts?.Cancel();
                    _watchCts?.Dispose();
                    _watchCts = null;
                    _isWatching = false;
                    return Result<string, string>.Success("⏹️ Stopped screen watching.");
                }

                _watchCts?.Dispose(); // Dispose previous instance if any
                _watchCts = new CancellationTokenSource();
                _isWatching = true;

                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _watchCts.Token);
                var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;

                // Start watching in background
                Task.Run(async () =>
                {
                    Bitmap? previousFrame = null;
                    var startTime = DateTime.Now;
                    int changeCount = 0;

                    try
                    {
                        while (!linkedCts.Token.IsCancellationRequested &&
                               (DateTime.Now - startTime).TotalSeconds < durationSeconds)
                        {
                            using var currentFrame = new Bitmap(screen.Width, screen.Height);
                            using var g = Graphics.FromImage(currentFrame);
                            g.CopyFromScreen(0, 0, 0, 0, screen.Size);

                            if (previousFrame != null)
                            {
                                var changePercent = CompareImages(previousFrame, currentFrame);
                                if (changePercent > sensitivity)
                                {
                                    changeCount++;
                                    var filename = $"change_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                                    var path = Path.Combine(CaptureDirectory, filename);
                                    Directory.CreateDirectory(CaptureDirectory);
                                    currentFrame.Save(path, ImageFormat.Png);

                                    OnScreenChanged?.Invoke($"Screen changed {changePercent:P1} - saved to {filename}");
                                }
                            }

                            previousFrame?.Dispose();
                            previousFrame = (Bitmap)currentFrame.Clone();

                            await Task.Delay(intervalMs, linkedCts.Token);
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        previousFrame?.Dispose();
                        linkedCts.Dispose();
                        _watchCts?.Dispose();
                        _watchCts = null;
                        _isWatching = false;
                    }
                }, linkedCts.Token)
                .ObserveExceptions("WatchScreen background");

                return Result<string, string>.Success($"👁️ Now watching screen for {durationSeconds}s (interval: {intervalMs}ms, sensitivity: {sensitivity:P0}).\n\nTo stop: call this tool again.\nChanges saved to: `{CaptureDirectory}`");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result<string, string>.Failure($"Screen watch failed: {ex.Message}");
            }
#else
            await Task.CompletedTask; // Suppress async warning
            return Result<string, string>.Failure("Screen watching is only supported on Windows");
#endif
        }

        private static double CompareImages(Bitmap img1, Bitmap img2)
        {
            // Simple pixel sampling comparison
            int sampleSize = 100;
            int differences = 0;
            var random = new Random(42);

            for (int i = 0; i < sampleSize; i++)
            {
                int x = random.Next(img1.Width);
                int y = random.Next(img1.Height);

                var p1 = img1.GetPixel(x, y);
                var p2 = img2.GetPixel(x, y);

                int diff = Math.Abs(p1.R - p2.R) + Math.Abs(p1.G - p2.G) + Math.Abs(p1.B - p2.B);
                if (diff > 30) differences++;
            }

            return (double)differences / sampleSize;
        }
    }
}
