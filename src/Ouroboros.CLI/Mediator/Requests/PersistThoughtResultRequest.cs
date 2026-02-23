using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to persist the result of a thought execution.
/// Replaces direct calls to <c>OuroborosAgent.PersistThoughtResultAsync</c>.
/// </summary>
public sealed record PersistThoughtResultRequest(
    Guid ThoughtId,
    string ResultType,
    string Content,
    bool Success,
    double Confidence,
    TimeSpan? ExecutionTime = null) : IRequest<Unit>;
