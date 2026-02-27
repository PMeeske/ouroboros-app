// <copyright file="GitProcessHelper.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Shared helper for running git commands as child processes.
/// </summary>
internal static class GitProcessHelper
{
    internal static async Task<(bool success, string output)> RunGitAsync(string workingDirectory, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process is null) return (false, string.Empty);

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode == 0, output);
        }
        catch
        {
            return (false, string.Empty);
        }
    }
}
