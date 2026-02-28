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
public sealed partial class OpenClawGatewayClient : IAsyncDisposable
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

    /// <summary>Gets a value indicating whether the client is currently reconnecting.</summary>
    public bool IsReconnecting { get; private set; }

    /// <summary>Gets the last reconnection error, if any.</summary>
    public Exception? LastReconnectError { get; private set; }

    /// <summary>Fired when a reconnection attempt fails after all retries.</summary>
    public event Action<Exception?>? OnReconnectionFailed;

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
            catch (OperationCanceledException) { throw; }
        catch (System.Net.WebSockets.WebSocketException ex)
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

}

/// <summary>
/// Exception representing an error response from the OpenClaw Gateway.
/// </summary>
public sealed class OpenClawException : Exception
{
    public OpenClawException(string message) : base(message) { }
    public OpenClawException(string message, Exception inner) : base(message, inner) { }
}
