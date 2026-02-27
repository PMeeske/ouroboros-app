// <copyright file="ScreenCaptureTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Drawing;
using System.Text.Json;
using Ouroboros.Core.Monads;

public static partial class PerceptionTools
{
    /// <summary>
    /// Capture the entire screen or a specific region.
    /// </summary>
    public class ScreenCaptureTool : ITool
    {
        public string Name => "capture_screen";
        public string Description => "Capture a screenshot of the screen. Input JSON (optional): {\"region\": {\"x\":0,\"y\":0,\"width\":800,\"height\":600}, \"monitor\": 0}. Returns path to saved image.";
        public string? JsonSchema => """{"type":"object","properties":{"region":{"type":"object","properties":{"x":{"type":"integer"},"y":{"type":"integer"},"width":{"type":"integer"},"height":{"type":"integer"}}},"monitor":{"type":"integer"}}}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            await Task.CompletedTask;
#if NET10_0_OR_GREATER_WINDOWS
            try
            {
                Directory.CreateDirectory(CaptureDirectory);

                // Parse optional region
                Rectangle? region = null;
                int monitor = 0;

                if (!string.IsNullOrWhiteSpace(input))
                {
                    try
                    {
                        var args = JsonSerializer.Deserialize<JsonElement>(input);
                        if (args.TryGetProperty("monitor", out var monEl))
                            monitor = monEl.GetInt32();
                        if (args.TryGetProperty("region", out var regEl))
                        {
                            region = new Rectangle(
                                regEl.GetProperty("x").GetInt32(),
                                regEl.GetProperty("y").GetInt32(),
                                regEl.GetProperty("width").GetInt32(),
                                regEl.GetProperty("height").GetInt32());
                        }
                    }
                    catch
                    {
                        // Invalid JSON input - use defaults (full screen, monitor 0)
                    }
                }

                // Get screen bounds
                var screens = System.Windows.Forms.Screen.AllScreens;
                if (monitor >= screens.Length) monitor = 0;
                var screenBounds = screens[monitor].Bounds;

                var captureBounds = region ?? screenBounds;

                using var bitmap = new Bitmap(captureBounds.Width, captureBounds.Height);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(captureBounds.X, captureBounds.Y, 0, 0, captureBounds.Size);

                var filename = $"screen_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var filepath = Path.Combine(CaptureDirectory, filename);
                bitmap.Save(filepath, ImageFormat.Png);

                return Result<string, string>.Success($"📸 Screenshot captured!\n\nSaved to: `{filepath}`\nSize: {captureBounds.Width}x{captureBounds.Height}\nMonitor: {monitor}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Screen capture failed: {ex.Message}");
            }
#else
            return Result<string, string>.Failure("Screen capture is only supported on Windows");
#endif
        }
    }
}
