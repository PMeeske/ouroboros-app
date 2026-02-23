using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="SayAndWaitRequest"/>.
/// Speaks text on the voice side channel and waits for completion.
/// Extracted from <c>OuroborosAgent.SayAndWaitAsync</c>.
/// </summary>
public sealed class SayAndWaitHandler : IRequestHandler<SayAndWaitRequest, Unit>
{
    private readonly OuroborosAgent _agent;

    public SayAndWaitHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<Unit> Handle(SayAndWaitRequest request, CancellationToken ct)
    {
        var cleanText = OuroborosAgent.StripToolResults(request.Text);
        if (string.IsNullOrWhiteSpace(cleanText)) return Unit.Value;

        var sideChannel = _agent.VoiceSub.SideChannel;
        if (sideChannel == null) return Unit.Value;

        await sideChannel.SayAndWaitAsync(cleanText, request.Persona ?? _agent.Config.Persona, ct);
        return Unit.Value;
    }
}
