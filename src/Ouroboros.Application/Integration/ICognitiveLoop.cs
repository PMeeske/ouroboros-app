// <copyright file="ICognitiveLoop.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;

namespace Ouroboros.Application.Integration;

using Ouroboros.Core.Monads;

/// <summary>
/// Interface for the autonomous cognitive loop.
/// Implements continuous perception-reason-act cycles for autonomous operation.
/// </summary>
public interface ICognitiveLoop
{
    /// <summary>
    /// Gets a value indicating whether the cognitive loop is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the autonomous cognitive loop with the given configuration.
    /// Runs continuously until stopped or cancelled.
    /// </summary>
    /// <param name="config">Configuration for the cognitive loop.</param>
    /// <param name="ct">Cancellation token to stop the loop.</param>
    /// <returns>Task representing the running loop.</returns>
    Task RunAsync(CognitiveLoopConfig config, CancellationToken ct = default);

    /// <summary>
    /// Stops the cognitive loop gracefully.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error message.</returns>
    Task<Result<Abstractions.Unit, string>> StopAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current state of the cognitive loop.
    /// </summary>
    /// <returns>Current cognitive state.</returns>
    CognitiveState GetCurrentState();

    /// <summary>
    /// Manually triggers a single cognitive cycle (perception → reasoning → action).
    /// Useful for debugging or controlled execution.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing cycle outcome or error message.</returns>
    Task<Result<CycleOutcome, string>> ExecuteSingleCycleAsync(CancellationToken ct = default);
}