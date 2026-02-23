using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for running the immersive persona mode.
/// Parallels <see cref="IOuroborosAgentService"/> for the immersive command.
/// </summary>
public interface IImmersiveModeService
{
    /// <summary>
    /// Runs the immersive persona session with the given configuration.
    /// </summary>
    Task RunAsync(ImmersiveConfig config, CancellationToken cancellationToken = default);
}
