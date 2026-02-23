using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="RecallRequest"/>.
/// Recalls conversation memories about a topic via the personality engine.
/// </summary>
public sealed class RecallHandler : IRequestHandler<RecallRequest, string>
{
    private readonly OuroborosAgent _agent;

    public RecallHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(RecallRequest request, CancellationToken ct)
    {
        var personalityEngine = _agent.MemorySub.PersonalityEngine;
        if (personalityEngine != null && personalityEngine.HasMemory)
        {
            var memories = await personalityEngine.RecallConversationsAsync(
                request.Topic, _agent.VoiceService.ActivePersona.Name, 5);
            if (memories.Any())
            {
                var recollections = memories.Take(3).Select(m => m.UserMessage);
                return "I remember: " + string.Join("; ", recollections);
            }
        }
        return $"I don't have specific memories about '{request.Topic}' yet.";
    }
}
