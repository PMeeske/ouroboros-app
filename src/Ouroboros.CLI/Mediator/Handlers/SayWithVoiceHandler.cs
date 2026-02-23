using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="SayWithVoiceRequest"/>.
/// Speaks text through the agent's <see cref="VoiceModeService"/> using
/// either normal speech or whisper style for inner thoughts.
/// </summary>
public sealed class SayWithVoiceHandler : IRequestHandler<SayWithVoiceRequest, Unit>
{
    private readonly OuroborosAgent _agent;

    public SayWithVoiceHandler(OuroborosAgent agent)
    {
        _agent = agent;
    }

    public async Task<Unit> Handle(SayWithVoiceRequest request, CancellationToken cancellationToken)
    {
        if (request.IsWhisper)
        {
            await _agent.VoiceService.WhisperAsync(request.Text);
        }
        else
        {
            await _agent.VoiceService.SayAsync(request.Text);
        }

        return Unit.Value;
    }
}
