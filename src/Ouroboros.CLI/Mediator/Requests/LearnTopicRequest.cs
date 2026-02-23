using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to learn about a topic, creating tools, skills, and MeTTa knowledge.
/// </summary>
public sealed record LearnTopicRequest(string Topic) : IRequest<string>;
