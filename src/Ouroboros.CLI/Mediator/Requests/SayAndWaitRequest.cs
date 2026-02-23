using MediatR;

namespace Ouroboros.CLI.Mediator;

public sealed record SayAndWaitRequest(string Text, string? Persona = null) : IRequest<Unit>;
