using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="ChatRequest"/>.
/// Extracted from <c>OuroborosAgent.ChatAsync</c>.
/// Delegates to the ChatSubsystem's ChatAsync method.
/// </summary>
public sealed class ChatHandler : IRequestHandler<ChatRequest, string>
{
    private readonly OuroborosAgent _agent;

    public ChatHandler(OuroborosAgent agent) => _agent = agent;

    public Task<string> Handle(ChatRequest request, CancellationToken cancellationToken)
        => _agent.ChatSub.ChatAsync(request.Input);
}
