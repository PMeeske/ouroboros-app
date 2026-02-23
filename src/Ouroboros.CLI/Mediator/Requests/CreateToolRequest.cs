using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to dynamically create a new tool by name.
/// </summary>
public sealed record CreateToolRequest(string ToolName) : IRequest<string>;
