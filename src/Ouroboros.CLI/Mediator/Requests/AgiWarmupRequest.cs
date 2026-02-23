using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to perform AGI warmup at startup.
/// Replaces direct calls to <c>OuroborosAgent.PerformAgiWarmupAsync</c>.
/// </summary>
public sealed record AgiWarmupRequest : IRequest<Unit>;
