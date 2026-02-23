using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Abstractions;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the room presence command. Delegates config binding to
/// <see cref="Options.RoomCommandOptions.BindConfig"/> and session lifecycle to
/// <see cref="IRoomModeService"/>.
/// Parallels <see cref="OuroborosCommandHandler"/> for the ouroboros command.
/// </summary>
public sealed class RoomCommandHandler : ICommandHandler<RoomConfig>
{
    private readonly IRoomModeService _roomService;
    private readonly ISpectreConsoleService _console;
    private readonly ILogger<RoomCommandHandler> _logger;

    public RoomCommandHandler(
        IRoomModeService roomService,
        ISpectreConsoleService console,
        ILogger<RoomCommandHandler> logger)
    {
        _roomService = roomService;
        _console = console;
        _logger = logger;
    }

    public async Task<int> HandleAsync(
        RoomConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _roomService.RunAsync(config, cancellationToken);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running room mode");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
