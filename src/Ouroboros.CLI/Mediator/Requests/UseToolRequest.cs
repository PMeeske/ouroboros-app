using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to invoke an existing tool by name with optional input.
/// </summary>
public sealed record UseToolRequest(string ToolName, string? Input) : IRequest<string>;
