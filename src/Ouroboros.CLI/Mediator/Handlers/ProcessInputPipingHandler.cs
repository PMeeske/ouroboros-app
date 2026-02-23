using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="ProcessInputPipingRequest"/>.
/// Delegates to the <see cref="PipeProcessingSubsystem"/> via the agent's
/// internal <c>PipeSub</c> accessor.
/// </summary>
public sealed class ProcessInputPipingHandler : IRequestHandler<ProcessInputPipingRequest, string>
{
    private readonly OuroborosAgent _agent;

    public ProcessInputPipingHandler(OuroborosAgent agent)
    {
        _agent = agent;
    }

    public Task<string> Handle(ProcessInputPipingRequest request, CancellationToken cancellationToken)
        => _agent.PipeSub.ProcessInputWithPipingAsync(request.Input, request.MaxPipeDepth);
}
