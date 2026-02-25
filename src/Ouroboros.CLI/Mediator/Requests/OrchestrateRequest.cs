using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that orchestrates a goal using the CLI orchestrator pipeline.
/// </summary>
public sealed record OrchestrateRequest(string Goal) : IRequest<string>;
