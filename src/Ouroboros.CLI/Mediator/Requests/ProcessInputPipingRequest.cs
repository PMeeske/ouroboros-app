using MediatR;

namespace Ouroboros.CLI.Mediator;

public sealed record ProcessInputPipingRequest(string Input, int MaxPipeDepth = 5) : IRequest<string>;
