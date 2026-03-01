// <copyright file="DiskInfoTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;

/// <summary>
/// Disk information.
/// </summary>
internal class DiskInfoTool : ITool
{
    public string Name => "disk_info";
    public string Description => "Get disk/drive information and space usage";
    public string? JsonSchema => null;

    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{"Drive",-6} {"Label",-20} {"Type",-12} {"Total",-12} {"Free",-12} {"Used %",-8}");
            sb.AppendLine(new string('-', 75));

            var skipped = 0;
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady) continue;
                    var usedPercent = 100.0 * (drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize;
                    sb.AppendLine($"{drive.Name,-6} {drive.VolumeLabel,-20} {drive.DriveType,-12} {ByteFormatter.Format(drive.TotalSize),-12} {ByteFormatter.Format(drive.TotalFreeSpace),-12} {usedPercent:F1}%");
                }
                catch
                {
                    // Drive not ready or access denied
                    skipped++;
                }
            }

            if (skipped > 0)
                sb.AppendLine($"\n(Skipped {skipped} inaccessible drive(s))");

            return Task.FromResult(Result<string, string>.Success(sb.ToString()));
        }
        catch (IOException ex)
        {
            return Task.FromResult(Result<string, string>.Failure(ex.Message));
        }
    }

}
