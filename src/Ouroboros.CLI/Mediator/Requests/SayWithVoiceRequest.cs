using MediatR;

namespace Ouroboros.CLI.Mediator;

public sealed record SayWithVoiceRequest(string Text, bool IsWhisper = false) : IRequest<Unit>;
