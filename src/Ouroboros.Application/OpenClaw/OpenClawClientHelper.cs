// <copyright file="OpenClawClientHelper.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.OpenClaw;

using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Shared helpers used by both <see cref="OpenClawGatewayClient"/> (operator role)
/// and <see cref="PcNode.OpenClawPcNode"/> (node role):
/// auto-approve pairing, nonce extraction, and fragmented-frame reading.
/// </summary>
internal static class OpenClawClientHelper
{
    /// <summary>
    /// Approves the most recent pending device pairing request via the openclaw CLI.
    /// On Windows, routes through cmd.exe so that the npm-global openclaw.cmd is resolved.
    /// </summary>
    internal static async Task<bool> TryAutoApprovePairingAsync(string? token, ILogger logger)
    {
        try
        {
            ProcessStartInfo psi;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // npm global installs on Windows are .cmd files; UseShellExecute=false
                // bypasses PATHEXT so "openclaw" won't resolve — route through cmd.exe.
                psi = new ProcessStartInfo("cmd.exe")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add("openclaw");
            }
            else
            {
                psi = new ProcessStartInfo("openclaw")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }

            psi.ArgumentList.Add("devices");
            psi.ArgumentList.Add("approve");
            psi.ArgumentList.Add("--latest");
            if (!string.IsNullOrEmpty(token))
            {
                psi.ArgumentList.Add("--token");
                psi.ArgumentList.Add(token);
            }

            // SECURITY: ArgumentList prevents shell injection from token value.
            using var proc = Process.Start(psi);
            if (proc == null) return false;

            // Read stdout and stderr concurrently to avoid deadlock on large output.
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            var output = await stdoutTask;
            var error = await stderrTask;

            if (proc.ExitCode == 0)
            {
                logger.LogInformation("[OpenClaw] Auto-approved pairing: {Output}", output.Trim());
                return true;
            }

            logger.LogWarning("[OpenClaw] Auto-approve failed (exit {Code}): {Error}",
                proc.ExitCode, error.Trim());
            return false;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("[OpenClaw] Auto-approve failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Extracts the nonce from a connect.challenge frame, trying common field locations.
    /// Logs a warning with the raw JSON if extraction fails.
    /// </summary>
    internal static string? ExtractNonce(string challengeJson, ILogger logger)
    {
        try
        {
            using var doc = JsonDocument.Parse(challengeJson);
            var root = doc.RootElement;

            // Bare string: the nonce IS the value
            if (root.ValueKind == JsonValueKind.String)
                return root.GetString();

            // Flat: { "nonce": "..." }
            if (root.TryGetProperty("nonce", out var n))
                return n.GetString();

            // OpenClaw envelope: { "payload": { "nonce": "..." } }
            if (root.TryGetProperty("payload", out var payload)
                && payload.TryGetProperty("nonce", out var pln))
                return pln.GetString();

            // RPC envelope: { "params": { "nonce": "..." } }
            if (root.TryGetProperty("params", out var prms)
                && prms.TryGetProperty("nonce", out var pn))
                return pn.GetString();

            // Nested: { "data": { "nonce": "..." } }
            if (root.TryGetProperty("data", out var data)
                && data.TryGetProperty("nonce", out var dn))
                return dn.GetString();

            // Nested: { "challenge": "..." } or { "challenge": { "nonce": "..." } }
            if (root.TryGetProperty("challenge", out var ch))
            {
                if (ch.ValueKind == JsonValueKind.String) return ch.GetString();
                if (ch.TryGetProperty("nonce", out var cn)) return cn.GetString();
            }
        }
        catch
        {
            // Non-JSON challenge — fall through to warning
        }

        logger.LogWarning("[OpenClaw] Could not extract nonce from challenge; raw: {Raw}",
            challengeJson.Length > 500 ? challengeJson[..500] + "\u2026" : challengeJson);
        return null;
    }

    /// <summary>
    /// Reads a complete WebSocket message, reassembling fragmented frames.
    /// Throws <see cref="WebSocketException"/> if the gateway closes mid-handshake.
    /// </summary>
    internal static async Task<string> ReadFullMessageAsync(ClientWebSocket ws, CancellationToken ct)
    {
        byte[] buffer = new byte[8192];
        StringBuilder accumulated = new();

        while (true)
        {
            var result = await ws.ReceiveAsync(buffer, ct);

            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("Gateway closed connection during handshake");

            accumulated.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

            if (result.EndOfMessage)
                return accumulated.ToString();
        }
    }
}
