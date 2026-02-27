// <copyright file="RebuildSelfTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;

/// <summary>
/// Trigger a rebuild of Ouroboros.
/// </summary>
internal class RebuildSelfTool : ITool
{
    public string Name => "rebuild_self";
    public string Description => "Trigger a rebuild of my own codebase after modifications. This compiles any changes I've made to my source code.";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var projectDir = Environment.CurrentDirectory;

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build --no-restore",
                WorkingDirectory = projectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
            {
                return Result<string, string>.Failure("Failed to start build process.");
            }

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            var sb = new StringBuilder();
            sb.AppendLine($"**Build completed** (exit code: {process.ExitCode})\n");

            if (process.ExitCode == 0)
            {
                sb.AppendLine("Build successful! My modifications are now compiled.");
                sb.AppendLine("\n**Note**: To use the new code, I need to be restarted.");
            }
            else
            {
                sb.AppendLine("Build failed. Errors:");
                sb.AppendLine("```");
                sb.AppendLine(error);
                sb.AppendLine("```");
            }

            // Include last 20 lines of output
            var outputLines = output.Split('\n').TakeLast(20);
            sb.AppendLine("\n**Build output (last 20 lines):**");
            sb.AppendLine("```");
            sb.AppendLine(string.Join("\n", outputLines));
            sb.AppendLine("```");

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Build failed: {ex.Message}");
        }
    }
}
