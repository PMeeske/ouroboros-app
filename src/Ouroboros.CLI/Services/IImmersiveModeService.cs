using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for running the immersive persona mode lifecycle.
/// Parallels <see cref="IOuroborosAgentService"/> for the ouroboros command.
/// </summary>
public interface IImmersiveModeService
{
    /// <summary>
    /// Runs the immersive persona mode with the given configuration.
    /// </summary>
    Task RunAsync(ImmersiveConfig config, CancellationToken cancellationToken = default);
}
