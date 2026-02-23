using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for running the ambient room presence mode lifecycle.
/// Parallels <see cref="IOuroborosAgentService"/> for the ouroboros command.
/// </summary>
public interface IRoomModeService
{
    /// <summary>
    /// Runs the ambient room presence mode with the given configuration.
    /// </summary>
    Task RunAsync(RoomConfig config, CancellationToken cancellationToken = default);
}
