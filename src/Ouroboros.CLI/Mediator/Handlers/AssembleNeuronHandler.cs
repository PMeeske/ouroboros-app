using MediatR;
using Ouroboros.CLI.Commands;
using Ouroboros.Domain.Autonomous;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="AssembleNeuronRequest"/>.
/// Delegates to the agent's SelfAssemblySubsystem to submit a blueprint
/// through the full assembly pipeline and return the assembled neuron.
/// </summary>
public sealed class AssembleNeuronHandler : IRequestHandler<AssembleNeuronRequest, Neuron?>
{
    private readonly OuroborosAgent _agent;

    public AssembleNeuronHandler(OuroborosAgent agent)
    {
        _agent = agent;
    }

    public async Task<Neuron?> Handle(AssembleNeuronRequest request, CancellationToken cancellationToken)
    {
        return await _agent.SelfAssemblySub.AssembleNeuronAsync(request.Blueprint, cancellationToken);
    }
}
