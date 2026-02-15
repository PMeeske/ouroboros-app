using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for handling Ouroboros agent lifecycle: bootstrap, run, and dispose.
/// </summary>
public interface IOuroborosAgentService
{
    /// <summary>
    /// Runs the Ouroboros agent with the given configuration.
    /// Handles loading IConfiguration, applying static config, creating the agent, running, and disposing.
    /// </summary>
    /// <param name="config">The fully populated OuroborosConfig built from parsed CLI options.</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    Task RunAgentAsync(OuroborosConfig config, CancellationToken cancellationToken = default);
}