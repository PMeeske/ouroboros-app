using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="RememberRequest"/>.
/// Stores a conversation memory via the personality engine.
/// </summary>
public sealed class RememberHandler : IRequestHandler<RememberRequest, string>
{
    private readonly OuroborosAgent _agent;

    public RememberHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(RememberRequest request, CancellationToken ct)
    {
        var personalityEngine = _agent.MemorySub.PersonalityEngine;
        if (personalityEngine != null && personalityEngine.HasMemory)
        {
            await personalityEngine.StoreConversationMemoryAsync(
                _agent.VoiceService.ActivePersona.Name,
                $"Remember: {request.Info}",
                "Memory stored.",
                "user_memory",
                "neutral",
                0.8);
            return "Got it, I'll remember that.";
        }
        return "I don't have memory storage set up, but I'll try to keep it in mind for this session.";
    }
}
