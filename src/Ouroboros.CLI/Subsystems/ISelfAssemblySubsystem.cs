// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Application.SelfAssembly;
using Ouroboros.Domain.Autonomous;

/// <summary>
/// Manages self-assembly: LLM-based neuron code generation, user approval flow,
/// assembly event handling, blueprint analysis, and neuron instantiation.
/// </summary>
public interface ISelfAssemblySubsystem : IAgentSubsystem
{
    /// <summary>Analyzes the network for capability gaps and proposes new neuron blueprints.</summary>
    Task<IReadOnlyList<NeuronBlueprint>> AnalyzeAndProposeNeuronsAsync(CancellationToken ct = default);

    /// <summary>Submits a blueprint through the full assembly pipeline and returns the assembled neuron.</summary>
    Task<Neuron?> AssembleNeuronAsync(NeuronBlueprint blueprint, CancellationToken ct = default);
}
