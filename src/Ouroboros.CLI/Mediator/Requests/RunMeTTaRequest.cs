using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that runs the MeTTa symbolic reasoning orchestrator.
/// </summary>
public sealed record RunMeTTaRequest(MeTTaConfig Config) : IRequest;
