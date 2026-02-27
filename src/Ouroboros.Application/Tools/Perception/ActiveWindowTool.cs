// <copyright file="ActiveWindowTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Diagnostics;
using System.Text;
using Ouroboros.Core.Monads;

public static partial class PerceptionTools
{
    /// <summary>
    /// Get info about the currently active window.
    /// </summary>
    public class ActiveWindowTool : ITool
    {
        public string Name => "get_active_window";
        public string Description => "Get information about the currently active window - title, process name, etc.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            try
            {
                var hwnd = GetForegroundWindow();
                var length = GetWindowTextLength(hwnd);
                var sb = new StringBuilder(length + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                var windowTitle = sb.ToString();

                GetWindowThreadProcessId(hwnd, out uint processId);
                var process = Process.GetProcessById((int)processId);

                var result = new StringBuilder();
                result.AppendLine("🪟 **Active Window**\n");
                result.AppendLine($"**Title:** {windowTitle}");
                result.AppendLine($"**Process:** {process.ProcessName}");
                result.AppendLine($"**PID:** {processId}");
                result.AppendLine($"**Path:** {GetProcessPath(process)}");
                result.AppendLine($"**Memory:** {process.WorkingSet64 / 1024 / 1024} MB");

                return Result<string, string>.Success(result.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get active window: {ex.Message}");
            }
        }

        private static string GetProcessPath(Process process)
        {
            try { return process.MainModule?.FileName ?? "Unknown"; }
            catch (InvalidOperationException) { return "Access denied"; }
            catch (System.ComponentModel.Win32Exception) { return "Access denied"; }
        }
    }
}
