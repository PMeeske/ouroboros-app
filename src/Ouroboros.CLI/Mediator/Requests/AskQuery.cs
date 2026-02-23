using MediatR;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR query that asks a question through the semantic CLI pipeline.
/// Replaces direct calls to <c>AskCommands.CreateSemanticCliPipeline</c> /
/// <c>AskCommands.RunAskAsync</c> from both <see cref="AskService"/> and
/// <c>OuroborosAgent</c>.
/// </summary>
public sealed record AskQuery(AskRequest Request) : IRequest<string>;
