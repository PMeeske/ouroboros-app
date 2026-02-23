using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Production implementation of <see cref="IRoomModeService"/>.
/// Adapts <see cref="RoomConfig"/> and delegates to <see cref="RoomMode.RunAsync"/>.
/// </summary>
public sealed class RoomModeService : IRoomModeService
{
    private readonly ILogger<RoomModeService> _logger;

    public RoomModeService(ILogger<RoomModeService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RunAsync(RoomConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting room mode — persona={Persona}, model={Model}, endpoint={Endpoint}",
            config.Persona, config.Model, config.Endpoint);

        var room = new RoomMode();
        await room.RunAsync(config, cancellationToken);

        _logger.LogInformation("Room mode session completed");
    }
}
