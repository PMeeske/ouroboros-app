using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for running the immersive persona mode.
/// Follows the same pattern as <see cref="IOuroborosAgentService"/>.
/// </summary>
public interface IImmersiveModeService
{
    /// <summary>
    /// Runs the immersive persona session with the given configuration.
    /// </summary>
    Task RunAsync(ImmersiveConfig config, CancellationToken cancellationToken = default);
}
