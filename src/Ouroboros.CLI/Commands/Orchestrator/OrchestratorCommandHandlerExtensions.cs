using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Extension methods for wiring the orchestrator command to its handler.
/// </summary>
public static class OrchestratorCommandHandlerExtensions
{
    public static Command ConfigureOrchestratorCommand(
        this Command command,
        IHost host,
        OrchestratorCommandOptions options,
        Option<bool> globalVoiceOption)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = host.Services.GetRequiredService<OrchestratorCommandHandler>();
            await handler.HandleAsync(
                goal:     parseResult.GetValue(options.GoalOption) ?? string.Empty,
                useVoice: parseResult.GetValue(globalVoiceOption),
                cancellationToken);
        });
        return command;
    }
}
