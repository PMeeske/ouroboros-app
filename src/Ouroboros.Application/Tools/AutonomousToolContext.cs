// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using Ouroboros.Application.Services;
using Ouroboros.Domain.Autonomous;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Default mutable implementation of <see cref="IAutonomousToolContext"/>.
/// Holds all shared state that autonomous tools need, replacing static singletons.
/// </summary>
public sealed class AutonomousToolContext : IAutonomousToolContext
{
    /// <inheritdoc/>
    public AutonomousCoordinator? Coordinator { get; set; }

    /// <inheritdoc/>
    public ParallelMeTTaThoughtStreams? MeTTaOrchestrator { get; set; }

    /// <inheritdoc/>
    public CliPipelineState? PipelineState { get; set; }

    /// <inheritdoc/>
    public Func<string, CancellationToken, Task<string>>? OllamaFunction { get; set; }

    /// <inheritdoc/>
    public Func<string, CancellationToken, Task<string>>? SearchFunction { get; set; }

    /// <inheritdoc/>
    public Func<string, CancellationToken, Task<string>>? EvaluateFunction { get; set; }

    /// <inheritdoc/>
    public Func<string, CancellationToken, Task<string>>? ReasonFunction { get; set; }

    /// <inheritdoc/>
    public Func<string, string, CancellationToken, Task<string>>? ExecuteToolFunction { get; set; }

    /// <inheritdoc/>
    public Func<string, CancellationToken, Task<string>>? SummarizeFunction { get; set; }

    /// <inheritdoc/>
    public Func<string, CancellationToken, Task<string>>? CritiqueFunction { get; set; }

    /// <inheritdoc/>
    public Func<string, string, double, CancellationToken, Task>? EpisodicExternalStoreFunc { get; set; }

    /// <inheritdoc/>
    public Func<string, int, CancellationToken, Task<IEnumerable<string>>>? EpisodicExternalRecallFunc { get; set; }

    /// <inheritdoc/>
    public Action<string>? CognitiveEmitFunc { get; set; }
}
