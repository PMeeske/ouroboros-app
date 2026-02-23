using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Extension methods for wiring the room command to its handler via DI.
/// Follows the same pattern as <see cref="OuroborosCommandHandlerExtensions"/>.
/// </summary>
public static class RoomCommandHandlerExtensions
{
    /// <summary>
    /// Configures the room command to use <see cref="RoomCommandHandler"/> via DI.
    /// Binds CLI parse results to <see cref="RoomConfig"/> via
    /// <see cref="RoomCommandOptions.BindConfig"/> and delegates to the handler.
    /// </summary>
    public static Command ConfigureRoomCommand(
        this Command command,
        IHost host,
        RoomCommandOptions options)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = host.Services.GetRequiredService<RoomCommandHandler>();
            var config = options.BindConfig(parseResult);
            await handler.HandleAsync(config, cancellationToken);
        });

        return command;
    }
}
