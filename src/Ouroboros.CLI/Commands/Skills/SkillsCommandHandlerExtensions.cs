using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Extension methods for wiring the skills command to its handler.
/// </summary>
public static class SkillsCommandHandlerExtensions
{
    public static Command ConfigureSkillsCommand(
        this Command command,
        IHost host,
        SkillsCommandOptions options,
        Option<bool> globalVoiceOption)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = host.Services.GetRequiredService<SkillsCommandHandler>();
            await handler.HandleAsync(
                list:     parseResult.GetValue(options.ListOption),
                fetch:    parseResult.GetValue(options.FetchOption),
                useVoice: parseResult.GetValue(globalVoiceOption),
                cancellationToken);
        });
        return command;
    }
}
