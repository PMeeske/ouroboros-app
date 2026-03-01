// <copyright file="OpenClawPcNode.Handshake.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Application.Extensions;

namespace Ouroboros.Application.OpenClaw.PcNode;

/// <summary>
/// Handshake, nonce extraction, pairing auto-approval, and reconnection logic.
/// </summary>
public sealed partial class OpenClawPcNode
{
    private async Task SendNodeHandshakeAsync(string? token, CancellationToken ct)
    {
        // Step 1: Read the challenge
        var challengeJson = await OpenClawClientHelper.ReadFullMessageAsync(_ws, ct);
        _logger.LogDebug("[OpenClaw PC Node] Challenge frame: {Challenge}", challengeJson);
        string? nonce = OpenClawClientHelper.ExtractNonce(challengeJson, _logger);
        _logger.LogDebug("[OpenClaw PC Node] Nonce extracted: {Nonce}",
            nonce != null ? nonce[..Math.Min(16, nonce.Length)] + "..." : "<null>");

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

            _logger.LogDebug("[OpenClaw PC Node] Handshake token field: {Token}",
                string.IsNullOrEmpty(tokenOrEmpty)
                    ? "<empty — signature will fail if gateway expects a bearer token>"
                    : tokenOrEmpty[..Math.Min(8, tokenOrEmpty.Length)] + "...");
            _logger.LogDebug("[OpenClaw PC Node] Signing payload: deviceId={DeviceId} role=node scopes={Scopes} nonce={Nonce}",
                _deviceIdentity.DeviceId[..Math.Min(16, _deviceIdentity.DeviceId.Length)], scopesCsv, nonce[..Math.Min(16, nonce.Length)]);

            var (sig, signedAt, nonceVal) = _deviceIdentity.SignHandshake(
                nonce,
                clientId: "gateway-client",
                clientMode: "node",
                role: "node",
                scopesCsv: scopesCsv,
                tokenOrEmpty: tokenOrEmpty);

            _logger.LogDebug("[OpenClaw PC Node] Signature computed: signedAt={SignedAt} sig={Sig}",
                signedAt, sig[..Math.Min(16, sig.Length)] + "...");

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
        var responseJson = await OpenClawClientHelper.ReadFullMessageAsync(_ws, ct);
        _logger.LogDebug("[OpenClaw PC Node] Handshake response: {Json}", responseJson);
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
            if (errMsg.Contains("pairing required", StringComparison.OrdinalIgnoreCase) ||
                errMsg.Contains("device signature", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[OpenClaw PC Node] Pairing/signature error — auto-approving via CLI");
                await OpenClawClientHelper.TryAutoApprovePairingAsync(token, _logger);
            }

            throw new OpenClawException(errMsg);
        }

        // Persist device token if issued
        if (_deviceIdentity != null
            && root.TryGetProperty("auth", out var authEl)
            && authEl.TryGetProperty("deviceToken", out var dtEl)
            && dtEl.GetString() is { Length: > 0 } newDeviceToken)
        {
            Task.Run(() => _deviceIdentity.SaveDeviceTokenAsync(newDeviceToken, CancellationToken.None))
                .ObserveExceptions("SaveDeviceToken");
        }
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
        catch (OperationCanceledException) { throw; }
        catch (System.Net.WebSockets.WebSocketException ex)
        {
            _logger.LogError("[OpenClaw PC Node] Reconnection failed: {Message}", ex.Message);
        }
    }
}
