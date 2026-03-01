// <copyright file="PresenceDetector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;

/// <summary>
/// Detects user presence via camera (face/motion detection), WiFi (device proximity),
/// and input activity. Implements <see cref="IDetectionModule"/> for use with
/// <see cref="MicroDetectionWorker"/>.
///
/// State machine: Unknown → Present → Absent (requires consecutive confirmation frames).
/// </summary>
public class PresenceDetector : IDetectionModule
{
    private readonly PresenceConfig _config;
    private DateTime _lastPresenceDetected = DateTime.MinValue;
    private DateTime _lastAbsenceDetected = DateTime.MinValue;
    private int _consecutivePresenceFrames;
    private int _consecutiveAbsenceFrames;
    private PresenceState _currentState = PresenceState.Unknown;
    private readonly HashSet<string> _knownDevices = [];
    private int _lastWifiDeviceCount;
    private long _lastNetworkBytes;
    private bool _firstNetworkCheck = true;
    private DateTime _lastDetection = DateTime.MinValue;
    private bool _disposed;
    private bool _monitoring;
    private CancellationTokenSource? _monitorCts;

    /// <summary>Raised when user presence is detected.</summary>
    public event Action<PresenceEvent>? OnPresenceDetected;

    /// <summary>Raised when user absence is detected.</summary>
    public event Action<PresenceEvent>? OnAbsenceDetected;

    /// <summary>Raised when the presence state changes.</summary>
    public event Action<PresenceState, PresenceState>? OnStateChanged;

    /// <summary>Gets the current presence state.</summary>
    public PresenceState CurrentState => _currentState;

    /// <summary>Gets whether the detector is actively monitoring.</summary>
    public bool IsMonitoring => _monitoring && !_disposed;

    /// <summary>Gets the last time presence was detected.</summary>
    public DateTime LastPresenceTime => _lastPresenceDetected;

    /// <inheritdoc />
    public string Name => "presence";

    /// <inheritdoc />
    public TimeSpan Interval => TimeSpan.FromSeconds(_config.CheckIntervalSeconds);

    /// <summary>
    /// Initializes a new instance of the <see cref="PresenceDetector"/> class.
    /// </summary>
    public PresenceDetector(PresenceConfig? config = null)
    {
        _config = config ?? new PresenceConfig();
    }

    /// <summary>Starts presence monitoring.</summary>
    public void Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PresenceDetector));
        _monitoring = true;
        _monitorCts = new CancellationTokenSource();
    }

    /// <summary>Stops presence monitoring.</summary>
    public Task StopAsync()
    {
        _monitoring = false;
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public bool IsReady()
    {
        return DateTime.UtcNow - _lastDetection >= Interval;
    }

    /// <inheritdoc />
    public async Task<DetectionEvent?> DetectAsync(CancellationToken ct)
    {
        _lastDetection = DateTime.UtcNow;

        var check = await CheckPresenceAsync(ct);
        return ProcessPresenceCheck(check);
    }

    /// <summary>
    /// Adds a known device MAC/IP to track for presence.
    /// </summary>
    public void AddKnownDevice(string deviceIdentifier)
    {
        _knownDevices.Add(deviceIdentifier.ToUpperInvariant());
    }

    /// <summary>
    /// Performs an immediate presence check.
    /// </summary>
    public async Task<PresenceCheckResult> CheckPresenceAsync(CancellationToken ct = default)
    {
        var result = new PresenceCheckResult { Timestamp = DateTime.UtcNow };

        // Check WiFi/network for nearby devices
        var wifiResult = await CheckWifiPresenceAsync(ct);
        result.WifiDevicesNearby = wifiResult.DeviceCount;
        result.WifiPresenceConfidence = wifiResult.Confidence;

        // Check for camera/motion (if available)
        var motionResult = await CheckCameraPresenceAsync(ct);
        result.MotionDetected = motionResult.MotionDetected;
        result.CameraConfidence = motionResult.Confidence;

        // Check for keyboard/mouse activity
        var activityResult = CheckInputActivity();
        result.RecentInputActivity = activityResult.HasRecentActivity;
        result.InputActivityConfidence = activityResult.Confidence;

        // Calculate overall presence probability
        var confidences = new List<double>();
        if (_config.UseWifi) confidences.Add(result.WifiPresenceConfidence);
        if (_config.UseCamera) confidences.Add(result.CameraConfidence);
        if (_config.UseInputActivity) confidences.Add(result.InputActivityConfidence);

        result.OverallConfidence = confidences.Count > 0
            ? confidences.Average()
            : 0.5;

        result.IsPresent = result.OverallConfidence >= _config.PresenceThreshold;

        return result;
    }

    private DetectionEvent? ProcessPresenceCheck(PresenceCheckResult check)
    {
        var previousState = _currentState;

        if (check.IsPresent)
        {
            _consecutivePresenceFrames++;
            _consecutiveAbsenceFrames = 0;

            if (_consecutivePresenceFrames >= _config.PresenceConfirmationFrames)
            {
                if (_currentState != PresenceState.Present)
                {
                    var oldState = _currentState;
                    _currentState = PresenceState.Present;
                    _lastPresenceDetected = DateTime.UtcNow;

                    OnStateChanged?.Invoke(oldState, _currentState);
                    OnPresenceDetected?.Invoke(new PresenceEvent
                    {
                        State = PresenceState.Present,
                        Timestamp = DateTime.UtcNow,
                        Confidence = check.OverallConfidence,
                        Source = DetermineSource(check),
                    });

                    return new DetectionEvent(
                        Name,
                        "presence",
                        check.OverallConfidence,
                        DateTime.UtcNow,
                        JsonSerializer.SerializeToElement(new
                        {
                            source = DetermineSource(check),
                            previousState = previousState.ToString(),
                            wifiDevices = check.WifiDevicesNearby,
                            motionDetected = check.MotionDetected,
                        }));
                }
            }
        }
        else
        {
            _consecutiveAbsenceFrames++;
            _consecutivePresenceFrames = 0;

            if (_consecutiveAbsenceFrames >= _config.AbsenceConfirmationFrames)
            {
                if (_currentState != PresenceState.Absent)
                {
                    var oldState = _currentState;
                    _currentState = PresenceState.Absent;
                    _lastAbsenceDetected = DateTime.UtcNow;

                    OnStateChanged?.Invoke(oldState, _currentState);
                    OnAbsenceDetected?.Invoke(new PresenceEvent
                    {
                        State = PresenceState.Absent,
                        Timestamp = DateTime.UtcNow,
                        Confidence = 1.0 - check.OverallConfidence,
                        Source = "timeout",
                    });

                    return new DetectionEvent(
                        Name,
                        "absence",
                        1.0 - check.OverallConfidence,
                        DateTime.UtcNow,
                        JsonSerializer.SerializeToElement(new
                        {
                            source = "timeout",
                            previousState = previousState.ToString(),
                        }));
                }
            }
        }

        return null;
    }

    private string DetermineSource(PresenceCheckResult check)
    {
        var sources = new List<string>();

        if (check.WifiPresenceConfidence > 0.5) sources.Add("wifi");
        if (check.CameraConfidence > 0.5) sources.Add("camera");
        if (check.InputActivityConfidence > 0.5) sources.Add("input");

        return string.Join("+", sources);
    }

    private async Task<(int DeviceCount, double Confidence)> CheckWifiPresenceAsync(CancellationToken ct)
    {
        try
        {
            var arpDevices = await GetArpDevicesAsync(ct);
            var deviceCount = arpDevices.Count;
            var knownDevicesFound = arpDevices.Count(d => _knownDevices.Contains(d.ToUpperInvariant()));

            // Bug 17 fix: use differential network activity instead of cumulative
            var activityDelta = GetNetworkActivityDelta();

            double confidence = 0.3;

            if (deviceCount > _lastWifiDeviceCount)
            {
                confidence += 0.2;
            }

            if (knownDevicesFound > 0)
            {
                confidence += 0.3;
            }

            if (activityDelta > 1000) // bytes since last check (not cumulative)
            {
                confidence += 0.2;
            }

            _lastWifiDeviceCount = deviceCount;

            return (deviceCount, Math.Min(confidence, 1.0));
        }
        catch
        {
            return (0, 0.3);
        }
    }

    private async Task<List<string>> GetArpDevicesAsync(CancellationToken ct)
    {
        var devices = new List<string>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "arp",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-a");

            // SECURITY: safe — hardcoded "arp" with ArgumentList
            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);

                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        devices.Add(parts[0]);
                        if (parts.Length > 1) devices.Add(parts[1]);
                    }
                }
            }
        }
        catch
        {
            // Silently fail - WiFi detection is best-effort
        }

        return devices;
    }

    /// <summary>
    /// Returns the differential network activity (bytes since last check) instead of
    /// cumulative totals. Handles counter resets gracefully.
    /// </summary>
    private long GetNetworkActivityDelta()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                            && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            long totalBytes = 0;
            foreach (var ni in interfaces)
            {
                var stats = ni.GetIPv4Statistics();
                totalBytes += stats.BytesReceived + stats.BytesSent;
            }

            if (_firstNetworkCheck)
            {
                _firstNetworkCheck = false;
                _lastNetworkBytes = totalBytes;
                return 0; // No baseline yet
            }

            long delta = totalBytes - _lastNetworkBytes;
            _lastNetworkBytes = totalBytes;

            // Handle counter reset (negative delta means counters wrapped)
            return delta >= 0 ? delta : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<(bool MotionDetected, double Confidence)> CheckCameraPresenceAsync(CancellationToken ct)
    {
        if (!_config.UseCamera)
        {
            return (false, 0.0);
        }

        try
        {
            var captureDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ouroboros", "captures");

            if (Directory.Exists(captureDir))
            {
                var recentCaptures = Directory.GetFiles(captureDir, "camera_*.jpg")
                    .Select(f => new FileInfo(f))
                    .Where(f => (DateTime.Now - f.LastWriteTime).TotalMinutes < 5)
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(2)
                    .ToList();

                if (recentCaptures.Count >= 2)
                {
                    // Bug 16 fix: Use byte-level sampling instead of just file size.
                    // Sample bytes at multiple offsets for more reliable motion detection.
                    var motionScore = await CompareFrameBytesAsync(
                        recentCaptures[0].FullName, recentCaptures[1].FullName, ct);

                    bool motionDetected = motionScore > 0.05; // 5% byte difference threshold
                    double confidence = Math.Min(motionScore * 10.0, 1.0); // Scale up

                    return (motionDetected, motionDetected ? Math.Max(confidence, 0.6) : 0.3);
                }
            }

            return (false, 0.3);
        }
        catch
        {
            return (false, 0.0);
        }
    }

    /// <summary>
    /// Compares two image files by sampling bytes at multiple offsets.
    /// Returns the fraction of sampled bytes that differ significantly (0.0-1.0).
    /// More reliable than simple file-size comparison for motion detection.
    /// </summary>
    private static async Task<double> CompareFrameBytesAsync(
        string path1, string path2, CancellationToken ct)
    {
        try
        {
            var bytes1 = await File.ReadAllBytesAsync(path1, ct);
            var bytes2 = await File.ReadAllBytesAsync(path2, ct);

            int minLen = Math.Min(bytes1.Length, bytes2.Length);
            if (minLen < 100) return 0.0;

            // Sample every 64th byte starting after the JPEG header (first 100 bytes)
            int sampleCount = 0;
            int diffCount = 0;
            const int sampleStride = 64;
            const int diffThreshold = 10; // Byte difference threshold

            for (int i = 100; i < minLen; i += sampleStride)
            {
                sampleCount++;
                if (Math.Abs(bytes1[i] - bytes2[i]) > diffThreshold)
                    diffCount++;
            }

            // Also factor in size difference
            double sizeRatio = (double)Math.Abs(bytes1.Length - bytes2.Length)
                / Math.Max(bytes1.Length, bytes2.Length);

            double byteDiffRatio = sampleCount > 0 ? (double)diffCount / sampleCount : 0.0;

            return Math.Max(byteDiffRatio, sizeRatio);
        }
        catch
        {
            return 0.0;
        }
    }

    private (bool HasRecentActivity, double Confidence) CheckInputActivity()
    {
#if NET10_0_OR_GREATER_WINDOWS
        try
        {
            var idleTime = GetIdleTime();
            var idleSeconds = idleTime.TotalSeconds;

            var hasActivity = idleSeconds < _config.InputIdleThresholdSeconds;
            var confidence = hasActivity
                ? Math.Max(0.9 - (idleSeconds / _config.InputIdleThresholdSeconds * 0.5), 0.5)
                : 0.2;

            return (hasActivity, confidence);
        }
        catch
        {
            return (false, 0.3);
        }
#else
        return (false, 0.3);
#endif
    }

#if NET10_0_OR_GREATER_WINDOWS
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    private static TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };

        if (GetLastInputInfo(ref info))
        {
            var idleTicks = Environment.TickCount - (int)info.dwTime;
            return TimeSpan.FromMilliseconds(idleTicks);
        }

        return TimeSpan.Zero;
    }
#endif

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
