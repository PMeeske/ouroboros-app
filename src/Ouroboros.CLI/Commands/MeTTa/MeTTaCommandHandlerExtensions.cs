using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.Options;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Extension methods for configuring the metta command.
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

            var opts = new MeTTaOptions
            {
                Goal           = parseResult.GetValue(options.GoalOption) ?? string.Empty,
                Culture        = parseResult.GetValue(options.CultureOption),
                Model          = parseResult.GetValue(options.Model.ModelOption) ?? "ministral-3:latest",
                Temperature    = parseResult.GetValue(options.Model.TemperatureOption),
                MaxTokens      = parseResult.GetValue(options.Model.MaxTokensOption),
                TimeoutSeconds = parseResult.GetValue(options.Model.TimeoutSecondsOption),
                Endpoint       = parseResult.GetValue(options.Endpoint.EndpointOption),
                ApiKey         = parseResult.GetValue(options.Endpoint.ApiKeyOption),
                EndpointType   = parseResult.GetValue(options.Endpoint.EndpointTypeOption),
                Debug          = parseResult.GetValue(options.Diagnostics.DebugOption),
                Embed          = parseResult.GetValue(options.Embedding.EmbedModelOption) ?? "nomic-embed-text",
                EmbedModel     = parseResult.GetValue(options.Embedding.EmbedModelOption) ?? "nomic-embed-text",
                QdrantEndpoint = parseResult.GetValue(options.Embedding.QdrantEndpointOption) ?? "http://localhost:6334",
                PlanOnly       = parseResult.GetValue(options.PlanOnlyOption),
                ShowMetrics    = parseResult.GetValue(options.ShowMetricsOption),
                Interactive    = parseResult.GetValue(options.InteractiveOption),
                Persona        = parseResult.GetValue(options.PersonaOption) ?? "Iaret",
                Voice          = parseResult.GetValue(globalVoiceOption),
                VoiceOnly      = parseResult.GetValue(options.Voice.VoiceOnlyOption),
                LocalTts       = parseResult.GetValue(options.Voice.LocalTtsOption),
                VoiceLoop      = parseResult.GetValue(options.Voice.VoiceLoopOption),
            };

            await handler.HandleAsync(opts, cancellationToken);
        });

        return command;
    }
}
