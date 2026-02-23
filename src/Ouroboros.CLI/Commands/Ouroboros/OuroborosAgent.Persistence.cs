// <copyright file="OuroborosAgent.Persistence.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using MediatR;
using Ouroboros.CLI.Mediator;

namespace Ouroboros.CLI.Commands;

public sealed partial class OuroborosAgent
{
    /// <summary>
    /// Persists a new thought to storage for future sessions.
    /// Uses neuro-symbolic relations when Qdrant is available.
    /// </summary>
    private Task PersistThoughtAsync(InnerThought thought, string? topic = null)
        => _mediator.Send(new PersistThoughtRequest(thought, topic));

    /// <summary>
    /// Persists the result of a thought execution (action taken, response generated, etc).
    /// </summary>
    private Task PersistThoughtResultAsync(
        Guid thoughtId,
        string resultType,
        string content,
        bool success,
        double confidence,
        TimeSpan? executionTime = null)
        => _mediator.Send(new PersistThoughtResultRequest(thoughtId, resultType, content, success, confidence, executionTime));

    /// <summary>
    /// Handles presence detection - greets user proactively if push mode enabled.
    /// </summary>
    private Task HandlePresenceDetectedAsync(PresenceEvent evt)
        => _mediator.Send(new HandlePresenceRequest(evt));

    /// <summary>
    /// Performs AGI warmup at startup - primes the model with examples for autonomous operation.
    /// </summary>
    private Task PerformAgiWarmupAsync()
        => _mediator.Send(new AgiWarmupRequest());

    // ── SelfAssembly (delegated to SelfAssemblySubsystem) ────────────────────

    /// <summary>Analyzes capability gaps and proposes new neurons.</summary>

    // ── SelfAssembly (delegated to SelfAssemblySubsystem) ────────────────────

    /// <summary>Analyzes capability gaps and proposes new neurons.</summary>
    public Task<IReadOnlyList<NeuronBlueprint>> AnalyzeAndProposeNeuronsAsync(CancellationToken ct = default)
        => _selfAssemblySub.AnalyzeAndProposeNeuronsAsync(ct);

    /// <summary>Attempts to assemble a neuron from a blueprint.</summary>
    public Task<Neuron?> AssembleNeuronAsync(NeuronBlueprint blueprint, CancellationToken ct = default)
        => _selfAssemblySub.AssembleNeuronAsync(blueprint, ct);
}
