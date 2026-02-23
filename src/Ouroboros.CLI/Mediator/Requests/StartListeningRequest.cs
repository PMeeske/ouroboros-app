using MediatR;

namespace Ouroboros.CLI.Mediator;

public sealed record StartListeningRequest : IRequest<Unit>;
