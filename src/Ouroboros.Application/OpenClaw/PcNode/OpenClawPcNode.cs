// <copyright file="OpenClawPcNode.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

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
public sealed partial class OpenClawPcNode : IAsyncDisposable
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
