using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Extension methods for wiring the immersive command to its handler via DI.
/// Parallels <see cref="OuroborosCommandHandlerExtensions"/> for the ouroboros command.
/// </summary>
public static class ImmersiveCommandHandlerExtensions
{
    /// <summary>
    /// Configures the immersive command to use <see cref="ImmersiveCommandHandler"/> via DI.
    /// Binds CLI parse results to <see cref="ImmersiveConfig"/> via
    /// <see cref="ImmersiveCommandOptions.BindConfig"/> and delegates to the handler.
    /// </summary>
    public static Command ConfigureImmersiveCommand(
        this Command command,
        IHost host,
        ImmersiveCommandOptions options,
        Option<bool> globalVoiceOption)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = host.Services.GetRequiredService<ImmersiveCommandHandler>();
            var config = options.BindConfig(parseResult, globalVoiceOption);
            await handler.HandleAsync(config, cancellationToken);
        });

        return command;
    }
}
