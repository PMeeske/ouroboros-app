// <copyright file="WatchUserActivityTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Text;
using System.Text.Json;
using Ouroboros.Application.Extensions;
using Ouroboros.Core.Monads;

public static partial class PerceptionTools
{
    /// <summary>
    /// Watch user activity (keyboard, mouse, window changes).
    /// </summary>
    public class WatchUserActivityTool : ITool
    {
        public string Name => "watch_user_activity";
        public string Description => "Monitor user activity - active window changes, idle time, etc. Input JSON: {\"duration_seconds\": 60, \"report_interval_seconds\": 10}";
        public string? JsonSchema => """{"type":"object","properties":{"duration_seconds":{"type":"integer"},"report_interval_seconds":{"type":"integer"}}}""";

        private static CancellationTokenSource? _activityCts;
        private static bool _isWatchingActivity = false;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                int durationSeconds = 60;
                int reportInterval = 10;

                if (!string.IsNullOrWhiteSpace(input))
                {
                    var args = JsonSerializer.Deserialize<JsonElement>(input);
                    if (args.TryGetProperty("duration_seconds", out var durEl))
                        durationSeconds = durEl.GetInt32();
                    if (args.TryGetProperty("report_interval_seconds", out var repEl))
                        reportInterval = repEl.GetInt32();
                }

                if (_isWatchingActivity)
                {
                    _activityCts?.Cancel();
                    _activityCts?.Dispose();
                    _activityCts = null;
                    _isWatchingActivity = false;
                    return Result<string, string>.Success("⏹️ Stopped activity monitoring.");
                }

                _activityCts?.Dispose(); // Dispose previous instance if any
                _activityCts = new CancellationTokenSource();
                _isWatchingActivity = true;
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _activityCts.Token);

                var activityLog = new List<(DateTime Time, string Activity)>();
                string lastWindow = "";

                Task.Run(async () =>
                {
                    var startTime = DateTime.Now;
                    POINT lastMousePos = default;

                    try
                    {
                        while (!linkedCts.Token.IsCancellationRequested &&
                               (DateTime.Now - startTime).TotalSeconds < durationSeconds)
                        {
                            // Check active window
                            var hwnd = GetForegroundWindow();
                            var sb = new StringBuilder(256);
                            GetWindowText(hwnd, sb, sb.Capacity);
                            var currentWindow = sb.ToString();

                            if (currentWindow != lastWindow && !string.IsNullOrEmpty(currentWindow))
                            {
                                var activity = $"Window: {currentWindow}";
                                activityLog.Add((DateTime.Now, activity));
                                OnUserActivity?.Invoke(activity);
                                lastWindow = currentWindow;
                            }

                            // Check mouse movement
                            GetCursorPos(out POINT mousePos);
                            if (Math.Abs(mousePos.X - lastMousePos.X) > 50 ||
                                Math.Abs(mousePos.Y - lastMousePos.Y) > 50)
                            {
                                activityLog.Add((DateTime.Now, $"Mouse moved to ({mousePos.X}, {mousePos.Y})"));
                                lastMousePos = mousePos;
                            }

                            await Task.Delay(500, linkedCts.Token);
                        }

                        // Save activity log
                        if (activityLog.Count > 0)
                        {
                            Directory.CreateDirectory(CaptureDirectory);
                            var logPath = Path.Combine(CaptureDirectory, $"activity_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                            var logContent = string.Join("\n", activityLog.Select(a => $"[{a.Time:HH:mm:ss}] {a.Activity}"));
                            await File.WriteAllTextAsync(logPath, logContent);
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        linkedCts.Dispose();
                        _activityCts?.Dispose();
                        _activityCts = null;
                        _isWatchingActivity = false;
                    }
                }, linkedCts.Token)
                .ObserveExceptions("WatchUserActivity background");

                return Result<string, string>.Success($"👀 Now monitoring user activity for {durationSeconds}s.\n\nTracking: window changes, mouse movement\nLogs saved to: `{CaptureDirectory}`\n\nTo stop: call this tool again.");
            }
        catch (IOException ex)
            {
                return Result<string, string>.Failure($"Activity monitoring failed: {ex.Message}");
            }
        }
    }
}
