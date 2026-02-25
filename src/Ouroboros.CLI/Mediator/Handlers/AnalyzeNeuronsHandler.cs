using MediatR;
using Ouroboros.Application.SelfAssembly;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="AnalyzeNeuronsRequest"/>.
/// Delegates to the agent's SelfAssemblySubsystem to analyze capability gaps
/// and propose new neuron blueprints.
/// </summary>
public sealed class AnalyzeNeuronsHandler : IRequestHandler<AnalyzeNeuronsRequest, IReadOnlyList<NeuronBlueprint>>
{
    private readonly OuroborosAgent _agent;

    public AnalyzeNeuronsHandler(OuroborosAgent agent)
    {
        _agent = agent;
    }

    public async Task<IReadOnlyList<NeuronBlueprint>> Handle(AnalyzeNeuronsRequest request, CancellationToken cancellationToken)
    {
        return await _agent.SelfAssemblySub.AnalyzeAndProposeNeuronsAsync(cancellationToken);
    }
}
