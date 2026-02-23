using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for running the ambient room presence mode.
/// Parallels <see cref="IOuroborosAgentService"/> for the room command.
/// </summary>
public interface IRoomModeService
{
    /// <summary>
    /// Runs the room presence session with the given configuration.
    /// </summary>
    Task RunAsync(RoomConfig config, CancellationToken cancellationToken = default);
}
