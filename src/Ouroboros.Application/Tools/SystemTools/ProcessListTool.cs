// <copyright file="ProcessListTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Diagnostics;
using System.Text;

/// <summary>
/// List running processes.
/// </summary>
internal class ProcessListTool : ITool
{
    public string Name => "list_processes";
    public string Description => "List running processes. Input: optional filter (e.g., 'chrome' or empty for all)";
    public string? JsonSchema => null;

    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var filter = input.Trim().ToLower();
            var processes = Process.GetProcesses()
                .Where(p => string.IsNullOrEmpty(filter) || p.ProcessName.ToLower().Contains(filter))
                .OrderByDescending(p =>
                {
                    try { return p.WorkingSet64; }
                    catch { return 0; } // Process exited or access denied
                })
                .Take(50)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"{"PID",-8} {"Name",-30} {"Memory",-12} {"CPU Time",-15}");
            sb.AppendLine(new string('-', 70));

            var skipped = 0;
            foreach (var p in processes)
            {
                try
                {
                    var mem = FormatSize(p.WorkingSet64);
                    var cpu = p.TotalProcessorTime.ToString(@"hh\:mm\:ss");
                    sb.AppendLine($"{p.Id,-8} {p.ProcessName,-30} {mem,-12} {cpu,-15}");
                }
                catch
                {
                    // Process exited or access denied during formatting
                    skipped++;
                }
            }

            if (skipped > 0)
                sb.AppendLine($"\n(Skipped {skipped} inaccessible process(es))");

            return Task.FromResult(Result<string, string>.Success(sb.ToString()));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure(ex.Message));
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
