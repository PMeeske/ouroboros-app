// <copyright file="OpenClawPcNode.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ouroboros.Application.OpenClaw.PcNode;

/// <summary>
/// PC node WebSocket client that registers this machine as an OpenClaw device node.
///
/// Connects to the gateway with <c>role: "node"</c> and <c>scopes: ["node.execute"]</c>,
/// advertises enabled capabilities during handshake, and dispatches incoming
/// <c>node.invoke</c> requests to <see cref="IPcNodeCapabilityHandler"/> implementations.
///
/// Runs independently from the operator connection in <see cref="OpenClawGatewayClient"/>,
/// sharing the same <see cref="OpenClawDeviceIdentity"/> for device identity.
/// </summary>
public sealed class OpenClawPcNode : IAsyncDisposable
{
    private ClientWebSocket _ws = new();
    private Uri? _gatewayUri;
    private string? _token;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveLoop;
    private readonly ILogger _logger;
    private readonly OpenClawResiliencePipeline _resilience;
    private readonly OpenClawDeviceIdentity? _deviceIdentity;
    private readonly PcNodeCapabilityRegistry _capabilities;
    private readonly PcNodeSecurityPolicy _security;
    private readonly PcNodeSecurityConfig _config;
    private readonly OpenClawAuditLog _auditLog;

    /// <summary>Gets a value indicating whether the node is connected to the gateway.</summary>
    public bool IsConnected => _ws.State == WebSocketState.Open;

    /// <summary>Gets the resilience pipeline for status monitoring.</summary>
    public OpenClawResiliencePipeline Resilience => _resilience;

    /// <summary>Gets the security configuration.</summary>
    public PcNodeSecurityConfig Config => _config;

    /// <summary>Gets the capability registry.</summary>
    public PcNodeCapabilityRegistry Capabilities => _capabilities;

    /// <summary>
    /// Fired when a capability at or above the approval threshold is invoked.
    /// Return <c>true</c> to approve, <c>false</c> to deny.
    /// </summary>
    public Func<ApprovalRequest, Task<bool>>? OnApprovalRequired { get; set; }

    /// <summary>
    /// Fired when a gateway event is received (not a node.invoke request).
    /// </summary>
    public event Action<OpenClawEvent>? OnEvent;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenClawPcNode"/> class.
    /// </summary>
    public OpenClawPcNode(
        PcNodeSecurityConfig config,
        OpenClawDeviceIdentity? deviceIdentity = null,
        OpenClawResilienceConfig? resilienceConfig = null,
        ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _deviceIdentity = deviceIdentity;
        _logger = logger ?? NullLogger.Instance;
        _resilience = new OpenClawResiliencePipeline(resilienceConfig, _logger);
        _auditLog = new OpenClawAuditLog();
        _security = new PcNodeSecurityPolicy(config, _auditLog);
        _capabilities = PcNodeCapabilityRegistry.CreateDefault(_security, config);
    }

    /// <summary>
    /// Connects to the OpenClaw Gateway and performs the node handshake.
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
            await SendNodeHandshakeAsync(token, innerCt);
        }, ct);

        // Start background receive loop
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);

        _logger.LogInformation("[OpenClaw PC Node] Connected to gateway at {Uri} with {Count} capabilities",
            gatewayUri, _config.EnabledCapabilities.Count);
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
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "PC Node disconnecting", CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[OpenClaw PC Node] Close error (non-fatal): {Message}", ex.Message);
            }
        }

        if (_receiveLoop != null)
        {
            try { await _receiveLoop; }
            catch (OperationCanceledException) { }
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

    /// <summary>Gets the audit log summary.</summary>
    public string GetAuditSummary() => _auditLog.GetSummary();

    /// <summary>Gets enabled capability names and their status.</summary>
    public IReadOnlyList<(string Name, string Description, PcNodeRiskLevel Risk, bool Enabled)> GetCapabilityStatus()
    {
        return _capabilities.GetCapabilities()
            .Select(c =>
            {
                var handler = _capabilities.GetHandler(c.Name);
                return (c.Name, c.Description,
                    handler?.RiskLevel ?? PcNodeRiskLevel.Low,
                    _config.EnabledCapabilities.Contains(c.Name));
            })
            .OrderBy(c => c.Item3)
            .ThenBy(c => c.Name)
            .ToList();
    }

    // ── Handshake ────────────────────────────────────────────────────────────────

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

        // Advertise enabled capabilities
        var capabilityDescriptors = _capabilities
            .GetEnabledCapabilities(_config)
            .Select(c => new { name = c.Name, description = c.Description, schema = c.ParameterSchema })
            .ToArray();

        var connectParams = new Dictionary<string, object>
        {
            ["minProtocol"] = 3,
            ["maxProtocol"] = 3,
            ["role"] = "node",
            ["scopes"] = new[] { "node.execute" },
            ["client"] = new
            {
                id = "ouroboros-pc-node",
                version = "1.0.0",
                platform,
                mode = "node",
                hostname = Environment.MachineName,
            },
            ["capabilities"] = capabilityDescriptors,
        };

        // Auth
        var authMap = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(token))
            authMap["token"] = token;
        if (_deviceIdentity?.DeviceToken is { Length: > 0 } dt)
            authMap["deviceToken"] = dt;
        if (authMap.Count > 0)
            connectParams["auth"] = authMap;

        // Device identity signing
        if (_deviceIdentity != null)
        {
            if (nonce != null)
            {
                var sortedScopes = new[] { "node.execute" };
                var scopesCsv = string.Join(",", sortedScopes);
                var tokenOrEmpty = token
                    ?? (_deviceIdentity.DeviceToken is { Length: > 0 } devTok ? devTok : null)
                    ?? "";
                var (sig, signedAt, nonceVal) = _deviceIdentity.SignHandshake(
                    nonce,
                    clientId: "ouroboros-pc-node",
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
            else
            {
                throw new OpenClawException(
                    $"Could not extract nonce from connect.challenge; " +
                    $"challenge frame was: {challengeJson}");
            }
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

    // ── Receive Loop ─────────────────────────────────────────────────────────────

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
                    _logger.LogWarning("[OpenClaw PC Node] Gateway closed connection");
                    _ = TryReconnectAsync();
                    break;
                }

                messageBuffer.AddRange(buffer.AsSpan(0, result.Count).ToArray());

                if (result.EndOfMessage)
                {
                    var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    messageBuffer.Clear();
                    _ = ProcessMessageAsync(json);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning("[OpenClaw PC Node] WebSocket error: {Message}", ex.Message);
                _ = TryReconnectAsync();
                break;
            }
        }
    }

    private async Task ProcessMessageAsync(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
            var method = root.TryGetProperty("method", out var methodProp) ? methodProp.GetString() : null;
            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;

            // Handle node.invoke requests
            if (type == "req" && method == "node.invoke" && id != null)
            {
                await HandleNodeInvokeAsync(id, root);
                return;
            }

            // Handle events
            if (type == "event")
            {
                var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() ?? "unknown" : "unknown";
                var payload = root.TryGetProperty("data", out var data) ? data.Clone() : root.Clone();
                OnEvent?.Invoke(new OpenClawEvent(eventType, payload, DateTime.UtcNow));
                return;
            }

            _logger.LogDebug("[OpenClaw PC Node] Unhandled message: {Type}/{Method}",
                type ?? "?", method ?? "?");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("[OpenClaw PC Node] Failed to parse message: {Message}", ex.Message);
        }
    }

    // ── Node Invoke Dispatch ─────────────────────────────────────────────────────

    private async Task HandleNodeInvokeAsync(string requestId, JsonElement root)
    {
        string? capability = null;
        string callerDeviceId = "unknown";

        try
        {
            var @params = root.TryGetProperty("params", out var p) ? p : default;
            capability = @params.TryGetProperty("command", out var cmdEl) ? cmdEl.GetString() : null;
            callerDeviceId = @params.TryGetProperty("callerDeviceId", out var callerEl)
                ? callerEl.GetString() ?? "unknown" : "unknown";
            var invokeParams = @params.TryGetProperty("params", out var ip) ? ip : default;

            if (string.IsNullOrEmpty(capability))
            {
                await SendResponseAsync(requestId, false, null, "Missing 'command' in node.invoke params");
                return;
            }

            // Get handler
            var handler = _capabilities.GetHandler(capability);
            if (handler == null)
            {
                await SendResponseAsync(requestId, false, null, $"Unknown capability: {capability}");
                return;
            }

            // Security policy check
            var verdict = _security.ValidateIncomingInvoke(
                callerDeviceId, capability, invokeParams.ValueKind != JsonValueKind.Undefined ? invokeParams : null,
                handler.RiskLevel);

            if (!verdict.IsAllowed)
            {
                await SendResponseAsync(requestId, false, null, $"Security policy denied: {verdict.Reason}");
                return;
            }

            // Approval check
            if (_security.RequiresApproval(handler.RiskLevel, handler.RequiresApproval))
            {
                var approved = await RequestApprovalAsync(new ApprovalRequest(
                    requestId, callerDeviceId, capability,
                    invokeParams.ToString(), handler.RiskLevel));

                if (!approved)
                {
                    _auditLog.LogDenied("pc_node_invoke", capability, callerDeviceId, "User denied approval");
                    await SendResponseAsync(requestId, false, null, "Operation denied by user");
                    return;
                }
            }

            // Execute
            var context = new PcNodeExecutionContext(requestId, callerDeviceId, DateTime.UtcNow, _auditLog);
            var result = await handler.ExecuteAsync(invokeParams, context, CancellationToken.None);

            if (result.Success)
            {
                // Scan outbound content for sensitive data
                var resultText = result.Data?.ToString() ?? "";
                var contentVerdict = _security.ValidateOutboundContent(resultText);
                if (!contentVerdict.IsAllowed)
                {
                    _auditLog.LogDenied("pc_node_result", capability, callerDeviceId,
                        $"Outbound content blocked: {contentVerdict.Reason}");
                    await SendResponseAsync(requestId, false, null,
                        $"Result blocked by security policy: {contentVerdict.Reason}");
                    return;
                }

                await SendResponseAsync(requestId, true, result.Data, null, result.Base64Payload);
            }
            else
            {
                await SendResponseAsync(requestId, false, null, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenClaw PC Node] Error handling invoke for {Capability}", capability ?? "?");
            await SendResponseAsync(requestId, false, null, $"Internal error: {ex.Message}");
        }
    }

    // ── Response Sending ─────────────────────────────────────────────────────────

    private async Task SendResponseAsync(
        string requestId,
        bool success,
        JsonElement? data,
        string? error,
        string? base64Payload = null)
    {
        var response = new Dictionary<string, object?>
        {
            ["type"] = "res",
            ["id"] = requestId,
            ["ok"] = success,
        };

        if (success)
        {
            var resultObj = new Dictionary<string, object?>();
            if (data.HasValue)
                resultObj["data"] = data.Value;
            if (base64Payload != null)
                resultObj["payload"] = base64Payload;
            response["result"] = resultObj;
        }
        else
        {
            response["error"] = new { message = error ?? "Unknown error" };
        }

        var json = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync();
        try
        {
            if (_ws.State == WebSocketState.Open)
                await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    // ── Approval ─────────────────────────────────────────────────────────────────

    private async Task<bool> RequestApprovalAsync(ApprovalRequest request)
    {
        if (OnApprovalRequired == null)
        {
            _logger.LogWarning("[OpenClaw PC Node] No approval handler registered; denying {Capability}",
                request.Capability);
            return false;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.ApprovalTimeoutSeconds));
            var approvalTask = OnApprovalRequired(request);
            var completed = await Task.WhenAny(approvalTask, Task.Delay(-1, cts.Token));
            return completed == approvalTask && await approvalTask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[OpenClaw PC Node] Approval timed out for {Capability}", request.Capability);
            return false;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

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

/// <summary>
/// Request for user approval of a high-risk PC node operation.
/// </summary>
/// <param name="RequestId">Unique request identifier.</param>
/// <param name="CallerDeviceId">Device ID of the remote caller.</param>
/// <param name="Capability">Capability being invoked (e.g. "system.run").</param>
/// <param name="Parameters">JSON parameters (may be truncated for display).</param>
/// <param name="RiskLevel">Risk classification of the capability.</param>
public sealed record ApprovalRequest(
    string RequestId,
    string CallerDeviceId,
    string Capability,
    string Parameters,
    PcNodeRiskLevel RiskLevel);

/// <summary>
/// An event received from the OpenClaw gateway.
/// </summary>
/// <param name="EventType">Event type (e.g. "message.received", "node.connected").</param>
/// <param name="Payload">Event payload as JSON.</param>
/// <param name="Timestamp">UTC time the event was received.</param>
public sealed record OpenClawEvent(
    string EventType,
    JsonElement Payload,
    DateTime Timestamp);
