using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to fetch research papers from arXiv and register a skill.
/// Replaces direct calls to <c>OuroborosAgent.FetchResearchAsync</c>.
/// </summary>
public sealed record FetchResearchRequest(string Query) : IRequest<string>;
