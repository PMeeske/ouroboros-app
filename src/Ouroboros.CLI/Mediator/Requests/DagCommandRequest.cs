using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that executes a DAG visualization/management sub-command.
/// </summary>
public sealed record DagCommandRequest(string SubCommand) : IRequest<string>;
