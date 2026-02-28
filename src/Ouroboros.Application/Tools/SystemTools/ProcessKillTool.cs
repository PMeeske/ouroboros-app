// <copyright file="ProcessKillTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Diagnostics;

/// <summary>
/// Kill a process.
/// </summary>
internal class ProcessKillTool : ITool
{
    public string Name => "kill_process";
    public string Description => "Kill a process by PID or name. Input: PID number or process name";
    public string? JsonSchema => null;

    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var trimmed = input.Trim();

            if (int.TryParse(trimmed, out var pid))
            {
                var process = Process.GetProcessById(pid);
                process.Kill();
                return Task.FromResult(Result<string, string>.Success($"Killed process {pid}"));
            }
            else
            {
                var processes = Process.GetProcessesByName(trimmed);
                if (processes.Length == 0)
                    return Task.FromResult(Result<string, string>.Failure($"No process found: {trimmed}"));

                foreach (var p in processes)
                    p.Kill();

                return Task.FromResult(Result<string, string>.Success($"Killed {processes.Length} process(es) named '{trimmed}'"));
            }
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(Result<string, string>.Failure(ex.Message));
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure(ex.Message));
        }
    }
}
