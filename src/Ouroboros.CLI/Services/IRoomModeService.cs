using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for running the ambient room presence mode.
/// Follows the same pattern as <see cref="IOuroborosAgentService"/>.
/// </summary>
public interface IRoomModeService
{
    /// <summary>
    /// Runs the room presence session with the given configuration.
    /// </summary>
    Task RunAsync(RoomConfig config, CancellationToken cancellationToken = default);
}
