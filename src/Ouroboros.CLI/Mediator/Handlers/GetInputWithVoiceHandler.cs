using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="GetInputWithVoiceRequest"/>.
/// Reads user input through the agent's <see cref="VoiceModeService"/>,
/// returning an empty string when no input is provided.
/// </summary>
public sealed class GetInputWithVoiceHandler : IRequestHandler<GetInputWithVoiceRequest, string>
{
    private readonly OuroborosAgent _agent;

    public GetInputWithVoiceHandler(OuroborosAgent agent)
    {
        _agent = agent;
    }

    public async Task<string> Handle(GetInputWithVoiceRequest request, CancellationToken cancellationToken)
    {
        return await _agent.VoiceService.GetInputAsync(request.Prompt, cancellationToken) ?? string.Empty;
    }
}
