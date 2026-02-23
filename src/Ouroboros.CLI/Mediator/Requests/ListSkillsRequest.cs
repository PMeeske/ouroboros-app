using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to list all registered skills.
/// </summary>
public sealed record ListSkillsRequest : IRequest<string>;
