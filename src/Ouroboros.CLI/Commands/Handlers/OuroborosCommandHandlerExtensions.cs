using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Extension methods for wiring the ouroboros command to its handler via DI.
/// Parallels <see cref="AskCommandHandlerExtensions"/> for the ask command.
/// </summary>
public static class OuroborosCommandHandlerExtensions
{
    /// <summary>
    /// Configures the ouroboros command to use <see cref="OuroborosCommandHandler"/> via DI.
    /// Binds CLI parse results to <see cref="OuroborosConfig"/> via
    /// <see cref="OuroborosCommandOptions.BindConfig"/> and delegates to the handler.
    /// </summary>
    public static Command ConfigureOuroborosCommand(
        this Command command,
        IHost host,
        OuroborosCommandOptions options,
        Option<bool> globalVoiceOption)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = host.Services.GetRequiredService<OuroborosCommandHandler>();
            var config = options.BindConfig(parseResult, globalVoiceOption);
            await handler.HandleAsync(config, cancellationToken);
        });

        return command;
    }
}
