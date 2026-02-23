using MediatR;
using Ouroboros.Application.SelfAssembly;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request that analyzes capability gaps and proposes new neuron blueprints.
/// Delegates to <see cref="Ouroboros.CLI.Subsystems.SelfAssemblySubsystem.AnalyzeAndProposeNeuronsAsync"/>.
/// </summary>
public sealed record AnalyzeNeuronsRequest : IRequest<IReadOnlyList<NeuronBlueprint>>;
