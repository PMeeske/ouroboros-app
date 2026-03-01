// <copyright file="CameraCaptureTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Diagnostics;
using Ouroboros.Core.Monads;

public static partial class PerceptionTools
{
    /// <summary>
    /// Capture from webcam/camera.
    /// </summary>
    public class CameraCaptureTool : ITool
    {
        public string Name => "capture_camera";
        public string Description => "Capture an image from the webcam/camera. Input (optional): camera index (default 0). Returns path to saved image.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                Directory.CreateDirectory(CaptureDirectory);

                int cameraIndex = 0;
                if (int.TryParse(input?.Trim(), out var idx))
                    cameraIndex = idx;

                var filename = $"camera_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var filepath = Path.Combine(CaptureDirectory, filename);

                // Use ffmpeg for camera capture (cross-platform)
                // Try different camera device names
                var cameraNames = new[]
                {
                    "Integrated Camera",
                    "Integrated Webcam",
                    "USB Camera",
                    "HD Webcam",
                    "Webcam"
                };

                bool captured = false;
                string lastError = "";

                foreach (var camName in cameraNames)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    psi.ArgumentList.Add("-f");
                    psi.ArgumentList.Add("dshow");
                    psi.ArgumentList.Add("-i");
                    psi.ArgumentList.Add($"video={camName}");
                    psi.ArgumentList.Add("-frames:v");
                    psi.ArgumentList.Add("1");
                    psi.ArgumentList.Add("-y");
                    psi.ArgumentList.Add(filepath);

                    // SECURITY: safe — hardcoded "ffmpeg" with ArgumentList for camera capture
                    using var process = Process.Start(psi);
                    if (process == null) continue;

                    await process.WaitForExitAsync(ct);
                    lastError = await process.StandardError.ReadToEndAsync(ct);

                    if (File.Exists(filepath) && new FileInfo(filepath).Length > 0)
                    {
                        captured = true;
                        break;
                    }
                }

                if (!captured)
                {
                    // Fallback: try listing available devices
                    var listPsi = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    listPsi.ArgumentList.Add("-list_devices");
                    listPsi.ArgumentList.Add("true");
                    listPsi.ArgumentList.Add("-f");
                    listPsi.ArgumentList.Add("dshow");
                    listPsi.ArgumentList.Add("-i");
                    listPsi.ArgumentList.Add("dummy");

                    // SECURITY: safe — hardcoded "ffmpeg" with ArgumentList for device listing
                    using var listProcess = Process.Start(listPsi);
                    if (listProcess != null)
                    {
                        var devices = await listProcess.StandardError.ReadToEndAsync(ct);
                        return Result<string, string>.Failure($"Camera capture failed. Available devices:\n{devices}\n\nLast error: {lastError}");
                    }

                    return Result<string, string>.Failure($"Camera capture failed: {lastError}");
                }

                return Result<string, string>.Success($"📷 Camera image captured!\n\nSaved to: `{filepath}`");
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure($"Camera capture failed: {ex.Message}\n\nMake sure ffmpeg is installed and a camera is connected.");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                return Result<string, string>.Failure($"Camera capture failed: {ex.Message}\n\nMake sure ffmpeg is installed and a camera is connected.");
            }
        }
    }
}
