// <copyright file="OpenClawGatewayClient.Internals.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Application.Extensions;

namespace Ouroboros.Application.OpenClaw;

/// <summary>
/// Handshake, receive loop, message processing, reconnection, and nonce extraction.
/// </summary>
public sealed partial class OpenClawGatewayClient
{
    private async Task SendConnectHandshakeAsync(string? token, CancellationToken ct)
    {
        // Step 1: Read the full connect.challenge message (handles fragmented frames)
        var challengeJson = await ReadFullMessageAsync(ct);
        _logger.LogWarning("[OpenClaw] Challenge frame: {Challenge}", challengeJson);

        string? nonce = ExtractNonce(challengeJson);

        // Step 2: Build and send the connect request
        var platform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows) ? "win32" :
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.OSX) ? "darwin" : "linux";

        var connectParams = new Dictionary<string, object>
        {
            ["minProtocol"] = 3,
            ["maxProtocol"] = 3,
            ["role"] = "operator",
            ["scopes"] = new[] { "operator.read", "operator.write", "operator.admin" },
            ["client"] = new { id = "gateway-client", version = "1.0.0", platform, mode = "backend" },
        };

        // Auth: bearer token and/or previously-issued device token
        var authMap = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(token))
            authMap["token"] = token;
        if (_deviceIdentity?.DeviceToken is { Length: > 0 } dt)
            authMap["deviceToken"] = dt;
        if (authMap.Count > 0)
            connectParams["auth"] = authMap;

        // Device identity (required by gateway — satisfies "device identity required")
        if (_deviceIdentity != null)
        {
            if (nonce != null)
            {
                // v2 payload: v2|deviceId|clientId|clientMode|role|scopesCsv|signedAtMs|tokenOrEmpty|nonce
                // Gateway uses params.scopes.join(",") — NO sorting — so we must preserve the order as-sent.
                var scopesCsv = string.Join(",", (string[])connectParams["scopes"]);
                var tokenOrEmpty = token
                    ?? (_deviceIdentity.DeviceToken is { Length: > 0 } devTok ? devTok : null)
                    ?? "";

                _logger.LogDebug("[OpenClaw] Handshake token field: {Token}",
                    string.IsNullOrEmpty(tokenOrEmpty)
                        ? "<empty — signature will fail if gateway expects a bearer token>"
                        : tokenOrEmpty[..Math.Min(8, tokenOrEmpty.Length)] + "...");
                _logger.LogDebug("[OpenClaw] Signing payload: deviceId={DeviceId} role=operator scopes={Scopes} nonce={Nonce}",
                    _deviceIdentity.DeviceId[..Math.Min(16, _deviceIdentity.DeviceId.Length)], scopesCsv, nonce[..Math.Min(16, nonce.Length)]);

                var (sig, signedAt, nonceVal) = _deviceIdentity.SignHandshake(
                    nonce,
                    clientId: "gateway-client",
                    clientMode: "backend",
                    role: "operator",
                    scopesCsv: scopesCsv,
                    tokenOrEmpty: tokenOrEmpty);

                _logger.LogDebug("[OpenClaw] Signature computed: signedAt={SignedAt} sig={Sig}",
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
            else
            {
                // Nonce extraction failed — throw so the challenge text surfaces in
                // the [!] OpenClaw: ... startup error rather than silently sending
                // an incomplete device object that the gateway will reject anyway.
                throw new OpenClawException(
                    $"Could not extract nonce from connect.challenge; " +
                    $"challenge frame was: {challengeJson}");
            }
        }

        var handshake = new
        {
            type = "req",
            id = "handshake",
            method = "connect",
            @params = connectParams,
        };

        var json = JsonSerializer.Serialize(handshake);
        var bytes = Encoding.UTF8.GetBytes(json);

        _logger.LogDebug("[OpenClaw] Sending handshake: {Json}", json);

        await _sendLock.WaitAsync(ct);
        try
        {
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }

        // Step 3: Read the full hello-ok response (handles fragmented frames)
        var responseJson = await ReadFullMessageAsync(ct);
        _logger.LogDebug("[OpenClaw] Handshake response: {Json}", responseJson);

        // Verify hello-ok
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("ok", out var ok) && !ok.GetBoolean())
        {
            var errMsg = root.TryGetProperty("error", out var err)
                && err.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Handshake rejected"
                : "Handshake rejected";

            // Auto-approve pairing on first connect, then throw so the
            // outer retry (OpenClawSharedState) retries with the now-approved device.
            if (errMsg.Contains("pairing required", StringComparison.OrdinalIgnoreCase) ||
                errMsg.Contains("device signature", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[OpenClaw] Pairing/signature error — auto-approving via CLI");
                await OpenClawClientHelper.TryAutoApprovePairingAsync(_token, _logger);
            }

            throw new OpenClawException(errMsg);
        }

        // Persist device token if the gateway issued one after pairing
        if (_deviceIdentity != null
            && root.TryGetProperty("auth", out var authEl)
            && authEl.TryGetProperty("deviceToken", out var dtEl)
            && dtEl.GetString() is { Length: > 0 } newDeviceToken)
        {
            Task.Run(() => _deviceIdentity.SaveDeviceTokenAsync(newDeviceToken, CancellationToken.None))
                .ObserveExceptions("SaveDeviceToken");
        }
    }

    private async Task<string> ReadFullMessageAsync(CancellationToken ct) =>
        await OpenClawClientHelper.ReadFullMessageAsync(_ws, ct);

    private string? ExtractNonce(string challengeJson) =>
        OpenClawClientHelper.ExtractNonce(challengeJson, _logger);

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[65536];
        using var messageBuffer = new MemoryStream();

        while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
        {
            try
            {
                var result = await _ws.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("[OpenClaw] Gateway closed connection");
                    _ = TryReconnectAsync();
                    break;
                }

                messageBuffer.Write(buffer, 0, result.Count);

                if (result.EndOfMessage)
                {
                    var json = Encoding.UTF8.GetString(
                        messageBuffer.GetBuffer(), 0, (int)messageBuffer.Length);
                    messageBuffer.SetLength(0);
                    ProcessMessage(json);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning("[OpenClaw] WebSocket error in receive loop: {Message}", ex.Message);
                _ = TryReconnectAsync();
                break;
            }
        }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check if this is a response to a pending request
            if (root.TryGetProperty("id", out var idProp))
            {
                var id = idProp.GetString();
                if (id != null && _pending.TryRemove(id, out var tcs))
                {
                    if (root.TryGetProperty("error", out var error))
                    {
                        tcs.TrySetException(new OpenClawException(
                            error.TryGetProperty("message", out var msg)
                                ? msg.GetString() ?? "Unknown gateway error"
                                : "Unknown gateway error"));
                    }
                    else
                    {
                        // Clone the result so it outlives the JsonDocument
                        var resultElement = root.TryGetProperty("result", out var r) ? r.Clone() : root.Clone();
                        tcs.TrySetResult(resultElement);
                    }

                    return;
                }
            }

            // Event frame (no matching request ID) — log and broadcast
            if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "event")
            {
                _logger.LogDebug("[OpenClaw] Event: {Json}", json.Length > 200 ? json[..200] + "..." : json);

                var eventName = root.TryGetProperty("event", out var evProp) ? evProp.GetString() ?? "" : "";
                var payload   = root.TryGetProperty("payload", out var plProp) ? plProp.Clone() : default;
                OnPushMessage?.Invoke(eventName, payload);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("[OpenClaw] Failed to parse gateway message: {Message}", ex.Message);
        }
    }

    private async Task TryReconnectAsync()
    {
        if (_gatewayUri == null) return;

        IsReconnecting = true;
        try
        {
            await _resilience.ExecuteReconnectAsync(async ct =>
            {
                _ws.Dispose();
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(_gatewayUri, ct);
                await SendConnectHandshakeAsync(_token, ct);
            }, CancellationToken.None);

            _logger.LogInformation("[OpenClaw] Reconnected to gateway");
            LastReconnectError = null;

            // Restart receive loop
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            _receiveCts = new CancellationTokenSource();
            _receiveLoop = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);
        }
        catch (OperationCanceledException) { throw; }
        catch (System.Net.WebSockets.WebSocketException ex)
        {
            _logger.LogError("[OpenClaw] Reconnection failed: {Message}", ex.Message);
            LastReconnectError = ex;
            OnReconnectionFailed?.Invoke(ex);
        }
        finally
        {
            IsReconnecting = false;
        }
    }
}
