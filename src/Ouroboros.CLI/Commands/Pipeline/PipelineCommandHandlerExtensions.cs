using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Extension methods for wiring the pipeline command to its handler.
/// </summary>
public static class PipelineCommandHandlerExtensions
{
    public static Command ConfigurePipelineCommand(
        this Command command,
        IHost host,
        PipelineCommandOptions options,
        Option<bool> globalVoiceOption)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = host.Services.GetRequiredService<PipelineCommandHandler>();
            await handler.HandleAsync(
                dsl:      parseResult.GetValue(options.DslOption) ?? string.Empty,
                useVoice: parseResult.GetValue(globalVoiceOption),
                cancellationToken);
        });
        return command;
    }
}
