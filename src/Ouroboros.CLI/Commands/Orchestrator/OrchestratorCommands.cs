using System.Diagnostics;
using LangChain.Providers.Ollama;
using Ouroboros.Application.Configuration;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Options;
using Spectre.Console;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.CLI.Commands;

public static class OrchestratorCommands
{
    public static async Task RunOrchestratorAsync(OrchestratorOptions o)
    {
        // Voice mode integration
        if (o.Voice)
        {
            await RunOrchestratorVoiceModeAsync(o);
            return;
        }

        AnsiConsole.Write(OuroborosTheme.ThemedRule("Smart Model Orchestrator"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("Intelligent Model Selection"));
        AnsiConsole.WriteLine();

        if (o.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");

        try
        {
            // Check for multi-model preset
            if (!string.IsNullOrWhiteSpace(o.Preset))
            {
                await RunWithPresetAsync(o);
                return;
            }

            OllamaProvider provider = new OllamaProvider();
            ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, false, o.Culture);

            (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.ResolveWithOverrides(
                o.Endpoint,
                o.ApiKey,
                o.EndpointType);

            IChatCompletionModel CreateModel(string modelName)
            {
                if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
                {
                    return ServiceFactory.CreateRemoteChatModel(endpoint, apiKey, modelName, settings, endpointType);
                }
                return new OllamaChatAdapter(new OllamaChatModel(provider, modelName), o.Culture);
            }

            IChatCompletionModel generalModel = CreateModel(o.Model);
            IChatCompletionModel coderModel = o.CoderModel != null ? CreateModel(o.CoderModel) : generalModel;
            IChatCompletionModel reasonModel = o.ReasonModel != null ? CreateModel(o.ReasonModel) : generalModel;

            string backend = (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
                ? $"remote-{endpointType.ToString().ToLowerInvariant()}"
                : "ollama-local";
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[INIT] Backend={backend} Endpoint={endpoint ?? "local"}")}");
            AnsiConsole.WriteLine();

            ToolRegistry tools = ToolRegistry.CreateDefault();
            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"✓ Tool registry created with {tools.Count} tools"));
            AnsiConsole.WriteLine();

            OrchestratorBuilder builder = new OrchestratorBuilder(tools, "general")
                .WithModel(
                    "general",
                    generalModel,
                    ModelType.General,
                    new[] { "conversation", "general-purpose", "versatile" },
                    maxTokens: o.MaxTokens,
                    avgLatencyMs: 1000)
                .WithModel(
                    "coder",
                    coderModel,
                    ModelType.Code,
                    new[] { "code", "programming", "debugging", "syntax" },
                    maxTokens: o.MaxTokens,
                    avgLatencyMs: 1500)
                .WithModel(
                    "reasoner",
                    reasonModel,
                    ModelType.Reasoning,
                    new[] { "reasoning", "analysis", "logic", "explanation" },
                    maxTokens: o.MaxTokens,
                    avgLatencyMs: 1200)
                .WithMetricTracking(true);

            OrchestratedChatModel orchestrator = builder.Build();

            await ExecuteOrchestratorAsync(orchestrator, builder, o);
        }
        catch (Exception ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("ECONNREFUSED"))
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} Ollama is not running. Please start Ollama before using the orchestrator.[/]");
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("   Run: ollama serve"));
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(OuroborosTheme.ThemedRule("Orchestrator Failed"));
            AnsiConsole.MarkupLine($"  [red]Error: {Markup.Escape(ex.Message)}[/]");
            if (o.Debug)
            {
                AnsiConsole.WriteException(ex);
            }
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Runs the orchestrator using a preconfigured multi-model preset.
    /// </summary>
    private static async Task RunWithPresetAsync(OrchestratorOptions o)
    {
        MultiModelPresetConfig? preset = MultiModelPresets.GetByName(o.Preset!);
        if (preset is null)
        {
            string available = string.Join(", ", MultiModelPresets.ListNames());
            AnsiConsole.MarkupLine($"  [red]Unknown preset '{Markup.Escape(o.Preset!)}'. Available presets: {Markup.Escape(available)}[/]");
            Environment.Exit(1);
            return;
        }

        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText($"[PRESET] {preset.Name}:")} {Markup.Escape(preset.Description)}");
        AnsiConsole.WriteLine();

        // Display model configuration
        var table = OuroborosTheme.ThemedTable("Role", "Provider", "Model", "");
        foreach (var slot in preset.Models)
        {
            string master = slot.Role.Equals(preset.MasterRole, StringComparison.OrdinalIgnoreCase) ? "[bold rgb(255,200,50)]MASTER[/]" : "";
            table.AddRow(
                Markup.Escape(slot.Role),
                Markup.Escape(slot.ProviderType),
                Markup.Escape(slot.ModelName),
                master);
        }
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Create models from preset
        Dictionary<string, IChatCompletionModel> models = MultiModelPresetFactory.CreateModels(preset, o.Culture);
        AnsiConsole.MarkupLine(OuroborosTheme.Ok($"✓ Created {models.Count} models from preset '{preset.Name}'"));
        AnsiConsole.WriteLine();

        ToolRegistry tools = ToolRegistry.CreateDefault();
        AnsiConsole.MarkupLine(OuroborosTheme.Ok($"✓ Tool registry created with {tools.Count} tools"));
        AnsiConsole.WriteLine();

        // Build orchestrator from preset models
        OrchestratorBuilder builder = new OrchestratorBuilder(tools, preset.MasterRole);

        foreach (var slot in preset.Models.Where(s => models.ContainsKey(s.Role)))
        {
            var model = models[slot.Role];

            ModelType modelType = slot.Role.ToLowerInvariant() switch
            {
                "coder" => ModelType.Code,
                "reasoner" => ModelType.Reasoning,
                _ => ModelType.General,
            };

            int maxTokens = slot.MaxTokens ?? preset.DefaultMaxTokens;
            builder = builder.WithModel(slot.Role, model, modelType, slot.Tags, maxTokens: maxTokens, avgLatencyMs: slot.AvgLatencyMs);
        }

        builder = builder.WithMetricTracking(preset.EnableMetrics);
        OrchestratedChatModel orchestrator = builder.Build();

        await ExecuteOrchestratorAsync(orchestrator, builder, o);
    }

    /// <summary>
    /// Common execution logic for the orchestrator (shared between preset and manual configuration).
    /// </summary>
    private static async Task ExecuteOrchestratorAsync(OrchestratedChatModel orchestrator, OrchestratorBuilder builder, OrchestratorOptions o)
    {
        AnsiConsole.MarkupLine(OuroborosTheme.Ok("✓ Orchestrator configured with multiple models"));
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText("Goal:")} {Markup.Escape(o.Goal)}");
        AnsiConsole.WriteLine();

        Stopwatch sw = Stopwatch.StartNew();
        string response = await orchestrator.GenerateTextAsync(o.Goal);
        sw.Stop();

        AnsiConsole.Write(OuroborosTheme.ThemedRule("Response"));
        AnsiConsole.WriteLine(response);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[timing] Execution time: {sw.ElapsedMilliseconds}ms"));

        if (o.ShowMetrics)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(OuroborosTheme.ThemedRule("Performance Metrics"));
            IModelOrchestrator underlyingOrchestrator = builder.GetOrchestrator();
            IReadOnlyDictionary<string, PerformanceMetrics> metrics = underlyingOrchestrator.GetMetrics();

            var metricsTable = OuroborosTheme.ThemedTable("Model", "Executions", "Avg Latency", "Success Rate", "Last Used");
            foreach ((string modelName, PerformanceMetrics metric) in metrics)
            {
                metricsTable.AddRow(
                    Markup.Escape(modelName),
                    $"{metric.ExecutionCount}",
                    $"{metric.AverageLatencyMs:F2}ms",
                    $"{metric.SuccessRate:P2}",
                    $"{metric.LastUsed:g}");
            }
            AnsiConsole.Write(metricsTable);
        }

        var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Happy);
        AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\n{face} Orchestrator execution completed successfully"));
    }

    /// <summary>
    /// Runs the orchestrator in voice mode with conversational interaction.
    /// </summary>
    private static async Task RunOrchestratorVoiceModeAsync(OrchestratorOptions o)
    {
        var voiceService = VoiceModeExtensions.CreateVoiceService(
            voice: true,
            persona: o.Persona,
            voiceOnly: o.VoiceOnly,
            localTts: o.LocalTts,
            voiceLoop: o.VoiceLoop,
            model: o.Model,
            endpoint: o.Endpoint ?? "http://localhost:11434");

        await voiceService.InitializeAsync();
        voiceService.PrintHeader("SMART ORCHESTRATOR");

        // Initialize orchestrator
        OllamaProvider provider = new OllamaProvider();
        ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, false);
        (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.ResolveWithOverrides(o.Endpoint, o.ApiKey, o.EndpointType);

        IChatCompletionModel CreateModel(string modelName)
        {
            if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
            {
                return ServiceFactory.CreateRemoteChatModel(endpoint, apiKey, modelName, settings, endpointType);
            }
            return new OllamaChatAdapter(new OllamaChatModel(provider, modelName));
        }

        IChatCompletionModel generalModel = CreateModel(o.Model);
        IChatCompletionModel coderModel = o.CoderModel != null ? CreateModel(o.CoderModel) : generalModel;
        IChatCompletionModel reasonModel = o.ReasonModel != null ? CreateModel(o.ReasonModel) : generalModel;

        ToolRegistry tools = ToolRegistry.CreateDefault();

        OrchestratorBuilder builder = new OrchestratorBuilder(tools, "general")
            .WithModel("general", generalModel, ModelType.General, new[] { "conversation", "general-purpose" }, maxTokens: o.MaxTokens, avgLatencyMs: 1000)
            .WithModel("coder", coderModel, ModelType.Code, new[] { "code", "programming" }, maxTokens: o.MaxTokens, avgLatencyMs: 1500)
            .WithModel("reasoner", reasonModel, ModelType.Reasoning, new[] { "reasoning", "analysis" }, maxTokens: o.MaxTokens, avgLatencyMs: 1200)
            .WithMetricTracking(true);

        OrchestratedChatModel orchestrator = builder.Build();

        await voiceService.SayAsync("Smart orchestrator ready! I'll automatically pick the best model for each task. What would you like me to help with?");

        // Initial goal if provided
        if (!string.IsNullOrWhiteSpace(o.Goal))
        {
            try
            {
                var response = await orchestrator.GenerateTextAsync(o.Goal);
                await voiceService.SayAsync(response);
            }
            catch (Exception ex)
            {
                await voiceService.SayAsync($"Something went wrong: {ex.Message}");
            }

            if (!o.VoiceLoop)
            {
                voiceService.Dispose();
                return;
            }
        }

        // Voice loop
        bool running = true;
        while (running)
        {
            var input = await voiceService.GetInputAsync("\n  Goal: ");
            if (string.IsNullOrWhiteSpace(input)) continue;

            if (VoiceModeExtensions.IsExitCommand(input))
            {
                await voiceService.SayAsync("Goodbye! The orchestrator is always here when you need multi-model intelligence.");
                running = false;
                continue;
            }

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                await voiceService.SayAsync("Just tell me what you need! I'll automatically route your request to the best model - coding questions go to the coder, reasoning to the reasoner, and general questions to the main model.");
                continue;
            }

            if (input.Equals("metrics", StringComparison.OrdinalIgnoreCase) || input.Equals("stats", StringComparison.OrdinalIgnoreCase))
            {
                var metrics = builder.GetOrchestrator().GetMetrics();
                var summary = string.Join(", ", metrics.Take(3).Select(m => $"{m.Key}: {m.Value.ExecutionCount} calls"));
                await voiceService.SayAsync($"Model usage so far: {summary}");
                continue;
            }

            try
            {
                var response = await orchestrator.GenerateTextAsync(input);
                await voiceService.SayAsync(response);
            }
            catch (Exception ex)
            {
                await voiceService.SayAsync($"Hmm, that didn't work: {ex.Message}");
            }
        }

        voiceService.Dispose();
    }
}
