// <copyright file="OpenClawGatewayClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ouroboros.Application.OpenClaw;

/// <summary>
/// Async-safe WebSocket client for the OpenClaw Gateway.
///
/// Connects as an <c>operator</c> role to the Gateway WebSocket endpoint,
/// correlates request/response frames by ID, and wraps all I/O through
/// <see cref="OpenClawResiliencePipeline"/> for retry + circuit breaker protection.
/// </summary>
public sealed class OpenClawGatewayClient : IAsyncDisposable
{
    private ClientWebSocket _ws = new();
    private Uri? _gatewayUri;
    private string? _token;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveLoop;
    private readonly ILogger _logger;
    private readonly OpenClawResiliencePipeline _resilience;
    private readonly OpenClawDeviceIdentity? _deviceIdentity;
    private int _requestId;

    /// <summary>Gets a value indicating whether the client is connected.</summary>
    public bool IsConnected => _ws.State == WebSocketState.Open;

    /// <summary>Gets the resilience pipeline for status monitoring.</summary>
    public OpenClawResiliencePipeline Resilience => _resilience;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenClawGatewayClient"/> class.
    /// </summary>
    public OpenClawGatewayClient(
        OpenClawDeviceIdentity? deviceIdentity = null,
        OpenClawResilienceConfig? resilienceConfig = null,
        ILogger? logger = null)
    {
        _deviceIdentity = deviceIdentity;
        _logger = logger ?? NullLogger.Instance;
        _resilience = new OpenClawResiliencePipeline(resilienceConfig, _logger);
    }

    /// <summary>
    /// Connects to the OpenClaw Gateway and performs the operator handshake.
    /// </summary>
    public async Task ConnectAsync(Uri gatewayUri, string? token, CancellationToken ct = default)
    {
        _gatewayUri = gatewayUri;
        _token = token;

        await _resilience.ExecuteConnectAsync(async innerCt =>
        {
            if (_ws.State != WebSocketState.None)
            {
                _ws.Dispose();
                _ws = new ClientWebSocket();
            }

            await _ws.ConnectAsync(gatewayUri, innerCt);
            await SendConnectHandshakeAsync(token, innerCt);
        }, ct);

        // Start background receive loop
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);

        _logger.LogInformation("[OpenClaw] Connected to gateway at {Uri}", gatewayUri);
    }

    /// <summary>
    /// Sends an RPC request and awaits the response.
    /// </summary>
    public async Task<JsonElement> SendRequestAsync(string method, object? @params = null, CancellationToken ct = default)
    {
        return await _resilience.ExecuteRpcAsync(async innerCt =>
        {
            var id = Interlocked.Increment(ref _requestId).ToString();
            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = tcs;

            try
            {
                var request = new
                {
                    type = "req",
                    id,
                    method,
                    @params,
                };

                var json = JsonSerializer.Serialize(request);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _sendLock.WaitAsync(innerCt);
                try
                {
                    await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, innerCt);
                }
                finally
                {
                    _sendLock.Release();
                }

                // Await response with cancellation
                using var reg = innerCt.Register(() => tcs.TrySetCanceled(innerCt));
                return await tcs.Task;
            }
            catch
            {
                _pending.TryRemove(id, out _);
                throw;
            }
        }, ct);
    }

    /// <summary>
    /// Disconnects from the Gateway gracefully.
    /// </summary>
    public async Task DisconnectAsync()
    {
        _receiveCts?.Cancel();

        if (_ws.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Ouroboros disconnecting", CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[OpenClaw] Close error (non-fatal): {Message}", ex.Message);
            }
        }

        if (_receiveLoop != null)
        {
            try { await _receiveLoop; }
            catch (OperationCanceledException) { }
        }

        // Fail all pending requests
        foreach (var kv in _pending)
        {
            kv.Value.TrySetCanceled();
            _pending.TryRemove(kv.Key, out _);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _receiveCts?.Dispose();
        _sendLock.Dispose();
        _ws.Dispose();
    }

    // ── Internals ────────────────────────────────────────────────────────────────

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
            ["scopes"] = new[] { "operator.read", "operator.write" },
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
                // Gateway uses: auth?.token ?? auth?.deviceToken ?? "" in the verified payload.
                var sortedScopes = ((string[])connectParams["scopes"]).OrderBy(s => s, StringComparer.Ordinal);
                var scopesCsv = string.Join(",", sortedScopes);
                var tokenOrEmpty = token
                    ?? (_deviceIdentity.DeviceToken is { Length: > 0 } devTok ? devTok : null)
                    ?? "";
                var (sig, signedAt, nonceVal) = _deviceIdentity.SignHandshake(
                    nonce,
                    clientId: "gateway-client",
                    clientMode: "backend",
                    role: "operator",
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

        // DEBUG — remove once handshake is stable
        Console.Error.WriteLine($"[OpenClaw DEBUG] Sending handshake: {json}");

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
        // DEBUG — remove once handshake is stable
        Console.Error.WriteLine($"[OpenClaw DEBUG] Handshake response: {responseJson}");

        // Verify hello-ok
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("ok", out var ok) && !ok.GetBoolean())
        {
            var errMsg = root.TryGetProperty("error", out var err)
                && err.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Handshake rejected"
                : "Handshake rejected";
            throw new OpenClawException(errMsg);
        }

        // Persist device token if the gateway issued one after pairing
        if (_deviceIdentity != null
            && root.TryGetProperty("auth", out var authEl)
            && authEl.TryGetProperty("deviceToken", out var dtEl)
            && dtEl.GetString() is { Length: > 0 } newDeviceToken)
        {
            _ = Task.Run(() => _deviceIdentity.SaveDeviceTokenAsync(newDeviceToken, CancellationToken.None));
        }
    }

    // Read a complete WebSocket message, reassembling fragmented frames.
    private async Task<string> ReadFullMessageAsync(CancellationToken ct)
    {
        byte[] buffer = new byte[8192];
        StringBuilder accumulated = new();

        while (true)
        {
            var result = await _ws.ReceiveAsync(buffer, ct);

            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("Gateway closed connection during handshake");

            accumulated.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

            if (result.EndOfMessage)
                return accumulated.ToString();
        }
    }

    // Extract the challenge nonce, trying common field locations.
    // Logs a warning with the raw JSON if extraction fails so the correct
    // field name can be identified without redeploying.
    private string? ExtractNonce(string challengeJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(challengeJson);
            var root = doc.RootElement;

            // Bare string: the nonce IS the value  e.g. "abc123"
            if (root.ValueKind == JsonValueKind.String)
                return root.GetString();

            // Flat: { "nonce": "..." }
            if (root.TryGetProperty("nonce", out var n))
                return n.GetString();

            // OpenClaw envelope: { "payload": { "nonce": "..." } }
            if (root.TryGetProperty("payload", out var payload))
            {
                if (payload.TryGetProperty("nonce", out var pln))
                    return pln.GetString();
            }

            // RPC envelope: { "params": { "nonce": "..." } }
            if (root.TryGetProperty("params", out var prms))
            {
                if (prms.TryGetProperty("nonce", out var pn))
                    return pn.GetString();
            }

            // Nested: { "data": { "nonce": "..." } }
            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("nonce", out var dn))
                    return dn.GetString();
            }

            // Nested: { "challenge": "..." } or { "challenge": { "nonce": "..." } }
            if (root.TryGetProperty("challenge", out var ch))
            {
                if (ch.ValueKind == JsonValueKind.String)
                    return ch.GetString();
                if (ch.TryGetProperty("nonce", out var cn))
                    return cn.GetString();
            }
        }
        catch
        {
            // Non-JSON challenge — log and fall through
        }

        _logger.LogWarning("[OpenClaw] Could not extract nonce from challenge; raw: {Raw}",
            challengeJson.Length > 500 ? challengeJson[..500] + "…" : challengeJson);
        return null;
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[65536];
        var messageBuffer = new List<byte>();

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

                messageBuffer.AddRange(buffer.AsSpan(0, result.Count).ToArray());

                if (result.EndOfMessage)
                {
                    var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    messageBuffer.Clear();
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

            // Event frame (no matching request ID) — log for now
            if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "event")
            {
                _logger.LogDebug("[OpenClaw] Event: {Json}", json.Length > 200 ? json[..200] + "..." : json);
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

            // Restart receive loop
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            _receiveCts = new CancellationTokenSource();
            _receiveLoop = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError("[OpenClaw] Reconnection failed: {Message}", ex.Message);
        }
    }
}

/// <summary>
/// Exception representing an error response from the OpenClaw Gateway.
/// </summary>
public sealed class OpenClawException : Exception
{
    public OpenClawException(string message) : base(message) { }
    public OpenClawException(string message, Exception inner) : base(message, inner) { }
}
