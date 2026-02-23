using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that executes a maintenance sub-command.
/// </summary>
public sealed record MaintenanceCommandRequest(string SubCommand) : IRequest<string>;
