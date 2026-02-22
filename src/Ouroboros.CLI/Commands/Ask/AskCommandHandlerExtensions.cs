using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Extension methods for registering the ask command handler and configuring the ask command.
/// </summary>
public static class AskCommandHandlerExtensions
{
    public static IServiceCollection AddAskCommandHandler(this IServiceCollection services)
    {
        services.AddScoped<AskCommandHandler>();
        return services;
    }

    public static Command ConfigureAskCommand(
        this Command command,
        IHost host,
        AskCommandOptions options,
        Option<bool> globalVoiceOption)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = host.Services.GetRequiredService<AskCommandHandler>();

            await handler.HandleAsync(
                question: parseResult.GetValue(options.QuestionOption) ?? string.Empty,
                rag: parseResult.GetValue(options.RagOption),
                useVoice: parseResult.GetValue(globalVoiceOption),
                cancellationToken);
        });

        return command;
    }
}