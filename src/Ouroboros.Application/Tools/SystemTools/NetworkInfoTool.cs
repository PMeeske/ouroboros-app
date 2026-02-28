// <copyright file="NetworkInfoTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;

/// <summary>
/// Network information.
/// </summary>
internal class NetworkInfoTool : ITool
{
    public string Name => "network_info";
    public string Description => "Get network information (IP addresses, adapters, connectivity)";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            ProcessStartInfo psi;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("/C");
                psi.ArgumentList.Add("ipconfig");
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add("ip addr 2>/dev/null || ifconfig 2>/dev/null || echo 'No network tools available'");
            }

            using var process = Process.Start(psi);
            if (process == null)
                return Result<string, string>.Failure("Failed to get network info");

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            // Also get external IP
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var externalIp = await http.GetStringAsync("https://api.ipify.org", ct);
                output += $"\nExternal IP: {externalIp}";
            }
            catch
            {
                // External IP check failed (offline, API unavailable, timeout) - optional feature
                output += "\nExternal IP: (unavailable)";
            }

            return Result<string, string>.Success(output);
        }
        catch (InvalidOperationException ex)
        {
            return Result<string, string>.Failure(ex.Message);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return Result<string, string>.Failure(ex.Message);
        }
    }
}
