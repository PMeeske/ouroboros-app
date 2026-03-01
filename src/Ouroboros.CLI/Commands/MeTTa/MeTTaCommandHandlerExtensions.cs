using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.Application.Configuration;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Extension methods for wiring the metta command to its handler.
/// </summary>
public static class MeTTaCommandHandlerExtensions
{
    public static Command ConfigureMeTTaCommand(
        this Command command,
        IHost host,
        MeTTaCommandOptions options,
        Option<bool> globalVoiceOption)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = host.Services.GetRequiredService<MeTTaCommandHandler>();

            var config = new MeTTaConfig(
                Goal:           parseResult.GetValue(options.GoalOption) ?? string.Empty,
                Culture:        parseResult.GetValue(options.CultureOption),
                Model:          parseResult.GetValue(options.Model.ModelOption) ?? "deepseek-v3.1:671b-cloud",
                Temperature:    parseResult.GetValue(options.Model.TemperatureOption),
                MaxTokens:      parseResult.GetValue(options.Model.MaxTokensOption),
                TimeoutSeconds: parseResult.GetValue(options.Model.TimeoutSecondsOption),
                Endpoint:       parseResult.GetValue(options.Endpoint.EndpointOption),
                ApiKey:         parseResult.GetValue(options.Endpoint.ApiKeyOption),
                EndpointType:   parseResult.GetValue(options.Endpoint.EndpointTypeOption),
                Debug:          parseResult.GetValue(options.Diagnostics.DebugOption),
                Embed:          parseResult.GetValue(options.Embedding.EmbedModelOption) ?? "nomic-embed-text",
                EmbedModel:     parseResult.GetValue(options.Embedding.EmbedModelOption) ?? "nomic-embed-text",
                QdrantEndpoint: parseResult.GetValue(options.Embedding.QdrantEndpointOption) ?? DefaultEndpoints.QdrantGrpc,
                PlanOnly:       parseResult.GetValue(options.PlanOnlyOption),
                ShowMetrics:    parseResult.GetValue(options.ShowMetricsOption),
                Interactive:    parseResult.GetValue(options.InteractiveOption),
                Persona:        parseResult.GetValue(options.PersonaOption) ?? "Iaret",
                Voice:          parseResult.GetValue(globalVoiceOption),
                VoiceOnly:      parseResult.GetValue(options.Voice.VoiceOnlyOption),
                LocalTts:       parseResult.GetValue(options.Voice.LocalTtsOption),
                VoiceLoop:      parseResult.GetValue(options.Voice.VoiceLoopOption));

            await handler.HandleAsync(config, cancellationToken);
        });

        return command;
    }
}
