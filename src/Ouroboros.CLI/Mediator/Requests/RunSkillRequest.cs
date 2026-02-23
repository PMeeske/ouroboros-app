using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to execute a registered skill by name.
/// </summary>
public sealed record RunSkillRequest(string SkillName) : IRequest<string>;
