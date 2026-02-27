// <copyright file="PowerShellTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Diagnostics;

/// <summary>
/// Execute shell commands. Uses the platform-appropriate shell:
/// <c>/bin/sh</c> on Linux/macOS, <c>cmd.exe</c> on Windows.
/// </summary>
internal class PowerShellTool : ITool
{
    public string Name => "shell";
    public string Description => "Execute shell commands. Input: command string. Uses /bin/sh on Linux/macOS, cmd.exe on Windows.";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var (shell, shellArgs) = Agent.AgentToolFactory.GetShellCommand(input);
            var psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = shellArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return Result<string, string>.Failure("Failed to start shell process");

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (!string.IsNullOrWhiteSpace(error))
                return Result<string, string>.Success($"{output}\n[STDERR]: {error}");

            return Result<string, string>.Success(output);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure(ex.Message);
        }
    }
}
