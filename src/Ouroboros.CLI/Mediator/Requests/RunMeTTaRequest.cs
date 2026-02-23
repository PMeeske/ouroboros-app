using MediatR;
using Ouroboros.Options;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that runs the MeTTa symbolic reasoning orchestrator.
/// </summary>
public sealed record RunMeTTaRequest(MeTTaOptions Options) : IRequest;
