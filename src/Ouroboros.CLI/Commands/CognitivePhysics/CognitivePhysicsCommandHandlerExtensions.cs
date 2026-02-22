using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Extension methods for wiring the cognitive-physics command to its handler.
/// </summary>
public static class CognitivePhysicsCommandHandlerExtensions
{
    public static Command ConfigureCognitivePhysicsCommand(
        this Command command,
        IHost host,
        CognitivePhysicsCommandOptions options,
        Option<bool> globalVoiceOption)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = host.Services.GetRequiredService<CognitivePhysicsCommandHandler>();
            await handler.HandleAsync(
                operation: parseResult.GetValue(options.OperationOption) ?? "shift",
                focus:     parseResult.GetValue(options.FocusOption) ?? "general",
                target:    parseResult.GetValue(options.TargetOption),
                targets:   parseResult.GetValue(options.TargetsOption),
                resources: parseResult.GetValue(options.ResourcesOption),
                verbose:   parseResult.GetValue(options.VerboseOption),
                cancellationToken);
        });
        return command;
    }
}
