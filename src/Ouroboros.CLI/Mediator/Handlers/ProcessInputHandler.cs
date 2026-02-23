using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="ProcessInputRequest"/>.
/// Delegates to <see cref="OuroborosAgent.ProcessInputAsync"/> which contains
/// the full command-routing switch. This is a thin wrapper — the routing logic
/// stays in the agent's RunLoop partial because it is the central dispatch hub.
/// </summary>
public sealed class ProcessInputHandler : IRequestHandler<ProcessInputRequest, string>
{
    private readonly OuroborosAgent _agent;

    public ProcessInputHandler(OuroborosAgent agent)
    {
        _agent = agent;
    }

    public Task<string> Handle(ProcessInputRequest request, CancellationToken cancellationToken)
        => _agent.ProcessInputAsync(request.Input);
}
