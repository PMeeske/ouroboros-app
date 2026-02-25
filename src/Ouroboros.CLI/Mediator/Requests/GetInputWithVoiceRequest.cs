using MediatR;

namespace Ouroboros.CLI.Mediator;

public sealed record GetInputWithVoiceRequest(string Prompt) : IRequest<string>;
