// <copyright file="OpenClawResiliencePipeline.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Ouroboros.Application.OpenClaw;

/// <summary>
/// Polly v8 resilience pipeline for OpenClaw Gateway communication.
///
/// Provides three composable resilience strategies:
///
///   1. <b>RPC Pipeline</b> — wraps individual Gateway RPC calls with:
///      • Timeout (per-call) — prevents hanging on unresponsive gateway
///      • Retry with exponential backoff + jitter — handles transient WebSocket/network failures
///      • Circuit breaker — stops hammering a dead gateway after repeated failures
///
///   2. <b>Connection Pipeline</b> — wraps the initial WebSocket connect with:
///      • Timeout — bounded connection attempt
///      • Retry with longer backoff — gateway may be starting up
///
///   3. <b>Reconnection Pipeline</b> — wraps auto-reconnect attempts with:
///      • Retry with capped exponential backoff — handles gateway restarts, network blips
///      • Circuit breaker — backs off completely if gateway is persistently unavailable
///
/// All strategies are configured via <see cref="OpenClawResilienceConfig"/> and
/// produce structured log output via <see cref="ILogger"/>.
/// </summary>
public sealed class OpenClawResiliencePipeline
{
    private readonly ResiliencePipeline _rpcPipeline;
    private readonly ResiliencePipeline _connectPipeline;
    private readonly ResiliencePipeline _reconnectPipeline;
    private readonly ILogger _logger;

    /// <summary>
    /// Gets the current circuit breaker state for the RPC pipeline.
    /// </summary>
    public CircuitState RpcCircuitState => _rpcCircuitBreakerStateProvider?.CircuitState ?? CircuitState.Closed;

    /// <summary>
    /// Gets the current circuit breaker state for the reconnection pipeline.
    /// </summary>
    public CircuitState ReconnectCircuitState => _reconnectCircuitBreakerStateProvider?.CircuitState ?? CircuitState.Closed;

    // Hold references to monitor circuit breaker state
    private readonly CircuitBreakerStateProvider? _rpcCircuitBreakerStateProvider;
    private readonly CircuitBreakerStateProvider? _reconnectCircuitBreakerStateProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenClawResiliencePipeline"/> class.
    /// </summary>
    public OpenClawResiliencePipeline(OpenClawResilienceConfig? config = null, ILogger? logger = null)
    {
        var cfg = config ?? new OpenClawResilienceConfig();
        _logger = logger ?? NullLogger.Instance;

        var rpcCbState = new CircuitBreakerStateProvider();
        var reconnectCbState = new CircuitBreakerStateProvider();
        _rpcCircuitBreakerStateProvider = rpcCbState;
        _reconnectCircuitBreakerStateProvider = reconnectCbState;

        _rpcPipeline = BuildRpcPipeline(cfg, rpcCbState);
        _connectPipeline = BuildConnectPipeline(cfg);
        _reconnectPipeline = BuildReconnectPipeline(cfg, reconnectCbState);
    }

    // ── Public Execution Methods ────────────────────────────────────────────────

    /// <summary>
    /// Executes a Gateway RPC call through the resilience pipeline.
    /// Applies: timeout → retry (with backoff + jitter) → circuit breaker.
    /// </summary>
    public async Task<T> ExecuteRpcAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        return await _rpcPipeline.ExecuteAsync(
            async token => await operation(token),
            ct);
    }

    /// <summary>
    /// Executes a fire-and-forget Gateway RPC call through the resilience pipeline.
    /// </summary>
    public async Task ExecuteRpcAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        await _rpcPipeline.ExecuteAsync(
            async token => { await operation(token); },
            ct);
    }

    /// <summary>
    /// Executes the initial WebSocket connection through the connection pipeline.
    /// Applies: timeout → retry with longer backoff.
    /// </summary>
    public async Task ExecuteConnectAsync(Func<CancellationToken, Task> connectAction, CancellationToken ct = default)
    {
        await _connectPipeline.ExecuteAsync(
            async token => { await connectAction(token); },
            ct);
    }

    /// <summary>
    /// Executes a reconnection attempt through the reconnection pipeline.
    /// Applies: retry with capped backoff → circuit breaker.
    /// </summary>
    public async Task ExecuteReconnectAsync(Func<CancellationToken, Task> reconnectAction, CancellationToken ct = default)
    {
        await _reconnectPipeline.ExecuteAsync(
            async token => { await reconnectAction(token); },
            ct);
    }

    /// <summary>
    /// Returns a human-readable status summary for diagnostics.
    /// </summary>
    public string GetStatusSummary()
    {
        return $"RPC circuit: {RpcCircuitState}, Reconnect circuit: {ReconnectCircuitState}";
    }

    // ── Pipeline Builders ───────────────────────────────────────────────────────

    private ResiliencePipeline BuildRpcPipeline(OpenClawResilienceConfig cfg, CircuitBreakerStateProvider cbState)
    {
        return new ResiliencePipelineBuilder()
            // Layer 1: Per-call timeout
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = cfg.RpcTimeout,
                OnTimeout = args =>
                {
                    _logger.LogWarning(
                        "[OpenClaw] RPC timeout after {Timeout}s",
                        cfg.RpcTimeout.TotalSeconds);
                    return default;
                },
            })
            // Layer 2: Retry with exponential backoff + jitter
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = cfg.RpcMaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = cfg.RpcRetryBaseDelay,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<WebSocketException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<IOException>()
                    .Handle<OperationCanceledException>(ex => ex is not TaskCanceledException { CancellationToken.IsCancellationRequested: true }),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "[OpenClaw] RPC retry #{Attempt} after {Delay}ms — {Exception}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "unknown");
                    return default;
                },
            })
            // Layer 3: Circuit breaker
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = cfg.CircuitBreakerFailureRatio,
                MinimumThroughput = cfg.CircuitBreakerMinThroughput,
                SamplingDuration = cfg.CircuitBreakerSamplingDuration,
                BreakDuration = cfg.CircuitBreakerBreakDuration,
                StateProvider = cbState,
                ShouldHandle = new PredicateBuilder()
                    .Handle<WebSocketException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<IOException>(),
                OnOpened = args =>
                {
                    _logger.LogError(
                        "[OpenClaw] Circuit OPEN — Gateway unreachable, breaking for {Duration}s. Reason: {Exception}",
                        args.BreakDuration.TotalSeconds,
                        args.Outcome.Exception?.Message ?? "threshold exceeded");
                    return default;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("[OpenClaw] Circuit CLOSED — Gateway connection restored");
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogInformation("[OpenClaw] Circuit HALF-OPEN — probing Gateway availability");
                    return default;
                },
            })
            .Build();
    }

    private ResiliencePipeline BuildConnectPipeline(OpenClawResilienceConfig cfg)
    {
        return new ResiliencePipelineBuilder()
            // Timeout for the connection attempt
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = cfg.ConnectTimeout,
                OnTimeout = args =>
                {
                    _logger.LogWarning(
                        "[OpenClaw] Connection timeout after {Timeout}s",
                        cfg.ConnectTimeout.TotalSeconds);
                    return default;
                },
            })
            // Retry — gateway may be starting up
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = cfg.ConnectMaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = cfg.ConnectRetryBaseDelay,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<WebSocketException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<IOException>()
                    .Handle<HttpRequestException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "[OpenClaw] Connect retry #{Attempt} after {Delay}ms — {Exception}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "unknown");
                    return default;
                },
            })
            .Build();
    }

    private ResiliencePipeline BuildReconnectPipeline(OpenClawResilienceConfig cfg, CircuitBreakerStateProvider cbState)
    {
        return new ResiliencePipelineBuilder()
            // Retry with capped exponential backoff
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = cfg.ReconnectMaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = cfg.ReconnectRetryBaseDelay,
                MaxDelay = cfg.ReconnectMaxDelay,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<WebSocketException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<IOException>()
                    .Handle<HttpRequestException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "[OpenClaw] Reconnect retry #{Attempt} (delay {Delay}s) — {Exception}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalSeconds,
                        args.Outcome.Exception?.Message ?? "unknown");
                    return default;
                },
            })
            // Circuit breaker for reconnection — backs off entirely if gateway is persistently down
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = cfg.ReconnectCircuitBreakerFailureRatio,
                MinimumThroughput = cfg.ReconnectCircuitBreakerMinThroughput,
                SamplingDuration = cfg.ReconnectCircuitBreakerSamplingDuration,
                BreakDuration = cfg.ReconnectCircuitBreakerBreakDuration,
                StateProvider = cbState,
                ShouldHandle = new PredicateBuilder()
                    .Handle<WebSocketException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<IOException>()
                    .Handle<HttpRequestException>(),
                OnOpened = args =>
                {
                    _logger.LogError(
                        "[OpenClaw] Reconnect circuit OPEN — stopping reconnection attempts for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return default;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("[OpenClaw] Reconnect circuit CLOSED — will attempt reconnection on next failure");
                    return default;
                },
            })
            .Build();
    }
}

/// <summary>
/// Monitors the circuit breaker state externally without breaking encapsulation.
/// Passed into <see cref="CircuitBreakerStrategyOptions.StateProvider"/>.
/// </summary>
public sealed class CircuitBreakerStateProvider
{
    /// <summary>
    /// Current circuit state. Updated by Polly's circuit breaker strategy.
    /// </summary>
    public CircuitState CircuitState { get; internal set; } = CircuitState.Closed;
}
