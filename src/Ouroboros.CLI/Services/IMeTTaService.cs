using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Service interface for MeTTa orchestrator operations.
/// Wraps the symbolic reasoning pipeline (plan → execute) and interactive REPL.
/// </summary>
public interface IMeTTaService
{
    /// <summary>
    /// Runs the MeTTa orchestrator with the given configuration.
    /// Handles interactive mode, voice mode, and standard plan-then-execute flow.
    /// </summary>
    Task RunAsync(MeTTaConfig config, CancellationToken cancellationToken = default);
}
