using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="StopListeningRequest"/>.
/// Cancels the listening CTS, disposes the enhanced listener, and resets state.
/// Extracted from <c>OuroborosAgent.StopListening</c>.
/// </summary>
public sealed class StopListeningHandler : IRequestHandler<StopListeningRequest, Unit>
{
    private readonly OuroborosAgent _agent;

    public StopListeningHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<Unit> Handle(StopListeningRequest request, CancellationToken ct)
    {
        var voiceSub = _agent.VoiceSub;
        if (!voiceSub.IsListening) return Unit.Value;

        voiceSub.ListeningCts?.Cancel();

        if (voiceSub.Listener != null)
        {
            await voiceSub.Listener.DisposeAsync();
            voiceSub.Listener = null;
        }

        voiceSub.IsListening = false;
        _agent.ConsoleOutput.WriteSystem(
            _agent.LocalizationSub.GetLocalizedString("listening_stop"));
        return Unit.Value;
    }
}
