using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to suggest matching skills for a given goal.
/// </summary>
public sealed record SuggestSkillsRequest(string Goal) : IRequest<string>;
