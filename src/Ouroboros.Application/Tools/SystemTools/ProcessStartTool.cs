// <copyright file="ProcessStartTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Diagnostics;
using System.Text.Json;

/// <summary>
/// Start a process.
/// </summary>
internal class ProcessStartTool : ITool
{
    public string Name => "start_process";
    public string Description => "Start a program. Input: JSON {\"program\":\"notepad.exe\", \"args\":\"\", \"wait\":false}";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            string program;
            string args = "";
            bool wait = false;

            if (input.TrimStart().StartsWith("{"))
            {
                var json = JsonSerializer.Deserialize<JsonElement>(input);
                program = json.GetProperty("program").GetString() ?? "";
                if (json.TryGetProperty("args", out var argsEl))
                    args = argsEl.GetString() ?? "";
                if (json.TryGetProperty("wait", out var waitEl))
                    wait = waitEl.GetBoolean();
            }
            else
            {
                program = input.Trim();
            }

            var psi = new ProcessStartInfo(program, args)
            {
                UseShellExecute = true
            };

            var process = Process.Start(psi);
            if (process == null)
                return Result<string, string>.Failure("Failed to start process");

            if (wait)
            {
                await process.WaitForExitAsync(ct);
                return Result<string, string>.Success($"Process {program} completed with exit code {process.ExitCode}");
            }

            return Result<string, string>.Success($"Started {program} (PID: {process.Id})");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure(ex.Message);
        }
    }
}
