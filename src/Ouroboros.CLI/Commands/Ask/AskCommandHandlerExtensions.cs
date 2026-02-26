using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Extension methods for configuring the ask command.
/// </summary>
public static class AskCommandHandlerExtensions
{
    public static Command ConfigureAskCommand(
        this Command command,
        IHost host,
        AskCommandOptions options,
        Option<bool> globalVoiceOption)
    {
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = host.Services.GetRequiredService<AskCommandHandler>();

            // Positional argument takes precedence over --question option
            var question = parseResult.GetValue(options.QuestionArgument)
                           ?? parseResult.GetValue(options.QuestionOption)
                           ?? string.Empty;

            var request = new AskRequest(
                Question:       question,
                UseRag:         parseResult.GetValue(options.RagOption),
                ModelName:      parseResult.GetValue(options.Model.ModelOption) ?? "llama3:latest",
                Endpoint:       parseResult.GetValue(options.Endpoint.EndpointOption),
                ApiKey:         parseResult.GetValue(options.Endpoint.ApiKeyOption),
                EndpointType:   parseResult.GetValue(options.Endpoint.EndpointTypeOption),
                Temperature:    parseResult.GetValue(options.Model.TemperatureOption),
                MaxTokens:      parseResult.GetValue(options.Model.MaxTokensOption),
                TimeoutSeconds: parseResult.GetValue(options.Model.TimeoutSecondsOption),
                Stream:         parseResult.GetValue(options.Model.StreamOption),
                Culture:        parseResult.GetValue(options.CultureOption),
                AgentMode:      parseResult.GetValue(options.AgentLoop.AgentOption),
                AgentModeType:  parseResult.GetValue(options.AgentLoop.AgentModeOption) ?? "lc",
                AgentMaxSteps:  parseResult.GetValue(options.AgentLoop.AgentMaxStepsOption),
                StrictModel:    parseResult.GetValue(options.Diagnostics.StrictModelOption),
                Router:         parseResult.GetValue(options.MultiModel.RouterOption) ?? "off",
                CoderModel:     parseResult.GetValue(options.MultiModel.CoderModelOption),
                SummarizeModel: parseResult.GetValue(options.MultiModel.SummarizeModelOption),
                ReasonModel:    parseResult.GetValue(options.MultiModel.ReasonModelOption),
                GeneralModel:   parseResult.GetValue(options.MultiModel.GeneralModelOption),
                EmbedModel:     parseResult.GetValue(options.Embedding.EmbedModelOption) ?? "nomic-embed-text",
                TopK:           parseResult.GetValue(options.TopKOption),
                Debug:          parseResult.GetValue(options.Diagnostics.DebugOption),
                JsonTools:      parseResult.GetValue(options.Diagnostics.JsonToolsOption),
                VoiceOnly:      parseResult.GetValue(options.Voice.VoiceOnlyOption),
                LocalTts:       parseResult.GetValue(options.Voice.LocalTtsOption),
                VoiceLoop:      parseResult.GetValue(options.Voice.VoiceLoopOption));

            await handler.HandleAsync(
                request,
                useVoice: parseResult.GetValue(globalVoiceOption),
                cancellationToken);
        });

        return command;
    }
}