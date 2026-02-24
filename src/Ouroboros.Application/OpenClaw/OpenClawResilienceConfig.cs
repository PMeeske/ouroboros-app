// <copyright file="OpenClawResilienceConfig.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.OpenClaw;

/// <summary>
/// Configuration for the OpenClaw Polly resilience pipeline.
/// Tunable knobs for retry, circuit breaker, and timeout behavior across
/// three pipeline contexts: RPC calls, initial connection, and reconnection.
/// </summary>
public sealed class OpenClawResilienceConfig
{
    // ── RPC Pipeline (individual Gateway calls) ─────────────────────────────────

    /// <summary>
    /// Per-call timeout for Gateway RPC requests.
    /// Default: 15 seconds (covers network round-trip + gateway processing).
    /// </summary>
    public TimeSpan RpcTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Maximum retry attempts for a failed RPC call.
    /// Default: 3 (total attempts = 4 including initial).
    /// </summary>
    public int RpcMaxRetries { get; init; } = 3;

    /// <summary>
    /// Base delay for exponential backoff on RPC retries.
    /// Actual delays: ~1s, ~2s, ~4s (with jitter).
    /// Default: 1 second.
    /// </summary>
    public TimeSpan RpcRetryBaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    // ── Circuit Breaker (RPC) ───────────────────────────────────────────────────

    /// <summary>
    /// Failure ratio threshold that triggers the circuit breaker.
    /// Default: 0.5 (50% of calls failing within sampling window).
    /// </summary>
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    /// <summary>
    /// Minimum number of calls in the sampling window before the circuit
    /// breaker evaluates the failure ratio. Prevents tripping on sparse traffic.
    /// Default: 5 calls.
    /// </summary>
    public int CircuitBreakerMinThroughput { get; init; } = 5;

    /// <summary>
    /// Duration of the sliding window for failure ratio calculation.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Duration the circuit stays open (rejecting all calls) before
    /// transitioning to half-open for a probe attempt.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);

    // ── Connection Pipeline (initial WebSocket connect) ─────────────────────────

    /// <summary>
    /// Timeout for the initial WebSocket connection attempt.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum retry attempts for the initial connection.
    /// Gateway may be starting up — allow more attempts.
    /// Default: 5.
    /// </summary>
    public int ConnectMaxRetries { get; init; } = 5;

    /// <summary>
    /// Base delay for exponential backoff on connection retries.
    /// Actual delays: ~2s, ~4s, ~8s, ~16s, ~32s (with jitter).
    /// Default: 2 seconds.
    /// </summary>
    public TimeSpan ConnectRetryBaseDelay { get; init; } = TimeSpan.FromSeconds(2);

    // ── Reconnection Pipeline (auto-reconnect after disconnect) ─────────────────

    /// <summary>
    /// Maximum retry attempts for reconnection after a disconnect.
    /// Default: 10 (covers ~17 minutes with capped backoff).
    /// </summary>
    public int ReconnectMaxRetries { get; init; } = 10;

    /// <summary>
    /// Base delay for exponential backoff on reconnection retries.
    /// Default: 3 seconds.
    /// </summary>
    public TimeSpan ReconnectRetryBaseDelay { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Maximum delay cap for reconnection backoff.
    /// Prevents unbounded wait times.
    /// Default: 120 seconds (2 minutes).
    /// </summary>
    public TimeSpan ReconnectMaxDelay { get; init; } = TimeSpan.FromSeconds(120);

    // ── Circuit Breaker (Reconnection) ──────────────────────────────────────────

    /// <summary>
    /// Failure ratio that triggers the reconnection circuit breaker.
    /// Default: 0.8 (80% — more tolerant since reconnection is expected to fail).
    /// </summary>
    public double ReconnectCircuitBreakerFailureRatio { get; init; } = 0.8;

    /// <summary>
    /// Minimum reconnection attempts before evaluating failure ratio.
    /// Default: 3.
    /// </summary>
    public int ReconnectCircuitBreakerMinThroughput { get; init; } = 3;

    /// <summary>
    /// Sampling window for the reconnection circuit breaker.
    /// Default: 60 seconds.
    /// </summary>
    public TimeSpan ReconnectCircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Break duration for the reconnection circuit breaker.
    /// Longer than RPC breaker since gateway may be doing an extended restart.
    /// Default: 120 seconds (2 minutes).
    /// </summary>
    public TimeSpan ReconnectCircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(120);

    // ── Factory Methods ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a config tuned for local development (short timeouts, fewer retries).
    /// </summary>
    public static OpenClawResilienceConfig CreateDevelopment() => new()
    {
        RpcTimeout = TimeSpan.FromSeconds(5),
        RpcMaxRetries = 2,
        RpcRetryBaseDelay = TimeSpan.FromMilliseconds(500),
        ConnectTimeout = TimeSpan.FromSeconds(5),
        ConnectMaxRetries = 3,
        ConnectRetryBaseDelay = TimeSpan.FromSeconds(1),
        ReconnectMaxRetries = 5,
        ReconnectMaxDelay = TimeSpan.FromSeconds(30),
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(10),
        ReconnectCircuitBreakerBreakDuration = TimeSpan.FromSeconds(30),
    };

    /// <summary>
    /// Creates a config tuned for production (longer timeouts, more retries, patient reconnection).
    /// </summary>
    public static OpenClawResilienceConfig CreateProduction() => new();

    /// <summary>
    /// Creates an aggressive config for always-on scenarios (autonomous mode, monitoring).
    /// More retries, longer reconnection, tighter circuit breaker sampling.
    /// </summary>
    public static OpenClawResilienceConfig CreateAlwaysOn() => new()
    {
        RpcMaxRetries = 5,
        ConnectMaxRetries = 8,
        ConnectRetryBaseDelay = TimeSpan.FromSeconds(3),
        ReconnectMaxRetries = int.MaxValue, // Never stop trying
        ReconnectMaxDelay = TimeSpan.FromSeconds(300), // Cap at 5 minutes
        ReconnectCircuitBreakerBreakDuration = TimeSpan.FromSeconds(300),
        ReconnectCircuitBreakerFailureRatio = 0.95, // Very tolerant
    };
}
