// <copyright file="SystemInfoTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;

/// <summary>
/// Get system information.
/// </summary>
internal class SystemInfoTool : ITool
{
    public string Name => "system_info";
    public string Description => "Get system information (OS, CPU, memory, uptime)";
    public string? JsonSchema => null;

    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== System Information ===");
            sb.AppendLine($"Computer Name: {Environment.MachineName}");
            sb.AppendLine($"User: {Environment.UserName}");
            sb.AppendLine($"Domain: {Environment.UserDomainName}");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
            sb.AppendLine($"Processors: {Environment.ProcessorCount}");
            sb.AppendLine($".NET Version: {Environment.Version}");
            sb.AppendLine($"System Directory: {Environment.SystemDirectory}");
            sb.AppendLine($"Current Directory: {Environment.CurrentDirectory}");

            // Memory info via GC
            var gcInfo = GC.GetGCMemoryInfo();
            sb.AppendLine($"Total Memory: {ByteFormatter.Format(gcInfo.TotalAvailableMemoryBytes)}");
            sb.AppendLine($"Process Memory: {ByteFormatter.Format(Environment.WorkingSet)}");

            // Uptime
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            sb.AppendLine($"System Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m");

            return Task.FromResult(Result<string, string>.Success(sb.ToString()));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure(ex.Message));
        }
    }

}
