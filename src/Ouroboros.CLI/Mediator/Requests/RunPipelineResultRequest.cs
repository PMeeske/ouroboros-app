using MediatR;
using Ouroboros.Abstractions.Monads;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request to execute a pipeline DSL expression.
/// Replaces direct calls to <c>OuroborosAgent.RunPipelineResultAsync</c>.
/// </summary>
public sealed record RunPipelineResultRequest(string Dsl) : IRequest<Result<string, string>>;
