using MediatR;

namespace Ouroboros.CLI.Mediator;

public sealed record StopListeningRequest : IRequest<Unit>;
