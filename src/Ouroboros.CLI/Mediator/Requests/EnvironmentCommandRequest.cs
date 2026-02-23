using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that executes an environment detection/configuration sub-command.
/// </summary>
public sealed record EnvironmentCommandRequest(string SubCommand) : IRequest<string>;
