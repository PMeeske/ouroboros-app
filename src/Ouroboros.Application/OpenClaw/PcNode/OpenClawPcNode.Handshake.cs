// <copyright file="OpenClawPcNode.Handshake.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Application.OpenClaw.PcNode;

/// <summary>
/// Handshake, nonce extraction, pairing auto-approval, and reconnection logic.
/// </summary>
public sealed partial class OpenClawPcNode
{
    private async Task SendNodeHandshakeAsync(string? token, CancellationToken ct)
    {
        // Step 1: Read the challenge
        var challengeJson = await ReadFullMessageAsync(ct);
        string? nonce = ExtractNonce(challengeJson);

        // Step 2: Build node connect request
        var platform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows) ? "win32" :
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.OSX) ? "darwin" : "linux";

        var connectParams = new Dictionary<string, object>
        {
            ["minProtocol"] = 3,
            ["maxProtocol"] = 3,
            ["role"] = "node",
            ["scopes"] = new[] { "node.execute" },
            ["client"] = new
            {
                id = "gateway-client",
                version = "1.0.0",
                platform,
                mode = "node",
            },
        };

        // Auth
        var authMap = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(token))
            authMap["token"] = token;
        if (_deviceIdentity?.DeviceToken is { Length: > 0 } dt)
            authMap["deviceToken"] = dt;
        if (authMap.Count > 0)
            connectParams["auth"] = authMap;

        // Device identity signing (required by gateway for node role)
        if (_deviceIdentity != null && nonce != null)
        {
            var sortedScopes = new[] { "node.execute" };
            var scopesCsv = string.Join(",", sortedScopes);
            var tokenOrEmpty = token
                ?? (_deviceIdentity.DeviceToken is { Length: > 0 } devTok ? devTok : null)
                ?? "";
            var (sig, signedAt, nonceVal) = _deviceIdentity.SignHandshake(
                nonce,
                clientId: "gateway-client",
                clientMode: "node",
                role: "node",
                scopesCsv: scopesCsv,
                tokenOrEmpty: tokenOrEmpty);
            connectParams["device"] = new
            {
                id = _deviceIdentity.DeviceId,
                publicKey = _deviceIdentity.PublicKeyBase64Url,
                signature = sig,
                signedAt,
                nonce = nonceVal,
            };
        }

        var handshake = new
        {
            type = "req",
            id = "node-handshake",
            method = "connect",
            @params = connectParams,
        };

        var json = JsonSerializer.Serialize(handshake);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(ct);
        try
        {
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }

        // Step 3: Read hello-ok response
        var responseJson = await ReadFullMessageAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("ok", out var ok) && !ok.GetBoolean())
        {
            var errMsg = root.TryGetProperty("error", out var err)
                && err.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Node handshake rejected"
                : "Node handshake rejected";

            // Auto-approve pairing on first connect, then throw so the
            // resilience pipeline retries with a fresh websocket + handshake.
            if (errMsg.Contains("pairing required", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[OpenClaw PC Node] Pairing required — auto-approving via CLI");
                await TryAutoApprovePairingAsync(token);
            }

            throw new OpenClawException(errMsg);
        }

        // Persist device token if issued
        if (_deviceIdentity != null
            && root.TryGetProperty("auth", out var authEl)
            && authEl.TryGetProperty("deviceToken", out var dtEl)
            && dtEl.GetString() is { Length: > 0 } newDeviceToken)
        {
            _ = Task.Run(() => _deviceIdentity.SaveDeviceTokenAsync(newDeviceToken, CancellationToken.None));
        }
    }

    /// <summary>
    /// Auto-approve the pending device pairing request via the openclaw CLI.
    /// </summary>
    private async Task<bool> TryAutoApprovePairingAsync(string? token)
    {
        try
        {
            var args = "devices approve --latest";
            if (!string.IsNullOrEmpty(token))
                args += $" --token {token}";

            var psi = new System.Diagnostics.ProcessStartInfo("openclaw", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return false;

            await proc.WaitForExitAsync();
            var output = await proc.StandardOutput.ReadToEndAsync();

            if (proc.ExitCode == 0)
            {
                _logger.LogInformation("[OpenClaw PC Node] Auto-approved pairing: {Output}", output.Trim());
                return true;
            }

            var error = await proc.StandardError.ReadToEndAsync();
            _logger.LogWarning("[OpenClaw PC Node] Auto-approve failed (exit {Code}): {Error}",
                proc.ExitCode, error.Trim());
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[OpenClaw PC Node] Auto-approve failed: {Message}", ex.Message);
            return false;
        }
    }

    private string? ExtractNonce(string challengeJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(challengeJson);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.String)
                return root.GetString();

            if (root.TryGetProperty("nonce", out var n))
                return n.GetString();

            if (root.TryGetProperty("payload", out var payload) && payload.TryGetProperty("nonce", out var pln))
                return pln.GetString();

            if (root.TryGetProperty("params", out var prms) && prms.TryGetProperty("nonce", out var pn))
                return pn.GetString();

            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("nonce", out var dn))
                return dn.GetString();

            if (root.TryGetProperty("challenge", out var ch))
            {
                if (ch.ValueKind == JsonValueKind.String) return ch.GetString();
                if (ch.TryGetProperty("nonce", out var cn)) return cn.GetString();
            }
        }
        catch
        {
            // Non-JSON challenge
        }

        _logger.LogWarning("[OpenClaw PC Node] Could not extract nonce from challenge; raw: {Raw}",
            challengeJson.Length > 500 ? challengeJson[..500] + "..." : challengeJson);
        return null;
    }

    private async Task TryReconnectAsync()
    {
        if (_gatewayUri == null) return;

        try
        {
            await _resilience.ExecuteReconnectAsync(async ct =>
            {
                _ws.Dispose();
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(_gatewayUri, ct);
                await SendNodeHandshakeAsync(_token, ct);
            }, CancellationToken.None);

            _logger.LogInformation("[OpenClaw PC Node] Reconnected to gateway");

            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            _receiveCts = new CancellationTokenSource();
            _receiveLoop = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError("[OpenClaw PC Node] Reconnection failed: {Message}", ex.Message);
        }
    }
}
