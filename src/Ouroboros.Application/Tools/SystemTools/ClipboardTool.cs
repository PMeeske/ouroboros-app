// <copyright file="ClipboardTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

/// <summary>
/// Clipboard access — uses stdin piping to avoid command injection.
/// </summary>
internal class ClipboardTool : ITool
{
    public string Name => "clipboard";
    public string Description => "Read/write clipboard. Input: JSON {\"action\":\"get|set\", \"text\":\"...\"}";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            string action = "get";
            string text = "";

            if (!string.IsNullOrWhiteSpace(input) && input.TrimStart().StartsWith("{"))
            {
                var args = JsonSerializer.Deserialize<JsonElement>(input);
                action = args.TryGetProperty("action", out var actEl) ? actEl.GetString() ?? "get" : "get";
                text = args.TryGetProperty("text", out var textEl) ? textEl.GetString() ?? "" : "";
            }

            if (action == "set" && !string.IsNullOrEmpty(text))
            {
                return await SetClipboard(text, ct);
            }
            else
            {
                return await GetClipboard(ct);
            }
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure(ex.Message);
        }
    }

    private static async Task<Result<string, string>> SetClipboard(string text, CancellationToken ct)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Use stdin piping to avoid command injection (no shell metachar interpretation)
            var psi = new ProcessStartInfo("clip")
            {
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null)
                return Result<string, string>.Failure("Failed to start clip process");
            await process.StandardInput.WriteAsync(text);
            process.StandardInput.Close();
            await process.WaitForExitAsync(ct);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var psi = new ProcessStartInfo("pbcopy")
            {
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null)
                return Result<string, string>.Failure("Failed to start pbcopy process");
            await process.StandardInput.WriteAsync(text);
            process.StandardInput.Close();
            await process.WaitForExitAsync(ct);
        }
        else
        {
            // Linux: try xclip first, fall back to xsel
            var psi = new ProcessStartInfo("xclip")
            {
                Arguments = "-selection clipboard",
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            try
            {
                using var process = Process.Start(psi);
                if (process == null)
                    throw new InvalidOperationException("xclip not available");
                await process.StandardInput.WriteAsync(text);
                process.StandardInput.Close();
                await process.WaitForExitAsync(ct);
            }
            catch
            {
                // Fall back to xsel
                var psi2 = new ProcessStartInfo("xsel")
                {
                    Arguments = "--clipboard --input",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process2 = Process.Start(psi2);
                if (process2 == null)
                    return Result<string, string>.Failure("No clipboard tool available (tried xclip, xsel)");
                await process2.StandardInput.WriteAsync(text);
                process2.StandardInput.Close();
                await process2.WaitForExitAsync(ct);
            }
        }

        return Result<string, string>.Success("Clipboard updated");
    }

    private static async Task<Result<string, string>> GetClipboard(CancellationToken ct)
    {
        ProcessStartInfo psi;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C powershell -NoProfile -Command Get-Clipboard",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            psi = new ProcessStartInfo
            {
                FileName = "pbpaste",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = "-c \"xclip -selection clipboard -o 2>/dev/null || xsel --clipboard --output 2>/dev/null\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        using var p = Process.Start(psi);
        if (p == null) return Result<string, string>.Failure("Failed to access clipboard");
        var result = await p.StandardOutput.ReadToEndAsync(ct);
        return Result<string, string>.Success(result.Trim());
    }
}
