// <copyright file="ImmersiveMode.Response.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.Abstractions.Agent;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Options;
using Spectre.Console;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

public sealed partial class ImmersiveMode
{
    private bool _llmMessagePrinted = false;

    private async Task<IChatCompletionModel> CreateChatModelAsync(IVoiceOptions options)
    {
        // If subsystems are configured, return the pre-initialized effective model
        if (HasSubsystems && _modelsSub != null)
        {
            var effective = _modelsSub.GetEffectiveModel();
            if (effective != null)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Using LLM from agent subsystem"));
                return effective;
            }
        }

        var settings = new ChatRuntimeSettings(0.8, 1024, 120, false);

        // Try remote CHAT_ENDPOINT if configured
        string? endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
        string? apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");

        IChatCompletionModel baseModel;

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey))
        {
            if (!_llmMessagePrinted)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [~] Using remote LLM: {options.Model} via {endpoint}"));
                _llmMessagePrinted = true;
            }
            baseModel = new HttpOpenAiCompatibleChatModel(endpoint, apiKey, options.Model, settings);
        }
        else
        {
            // Use Ollama cloud model with the configured endpoint
            if (!_llmMessagePrinted)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [~] Using Ollama LLM: {options.Model} via {options.Endpoint}"));
                _llmMessagePrinted = true;
            }
            baseModel = new OllamaCloudChatModel(options.Endpoint, "ollama", options.Model, settings);
        }

        // Store base model for orchestration
        _baseModel = baseModel;

        // Initialize multi-model orchestration if specialized models are configured via environment
        await InitializeImmersiveOrchestrationAsync(options, settings, endpoint, apiKey);

        // Return orchestrated model if available, otherwise base model
        return _orchestratedModel ?? baseModel;
    }

    /// <summary>
    /// Initializes multi-model orchestration for immersive mode.
    /// Uses environment variables for specialized model configuration.
    /// </summary>
    private async Task InitializeImmersiveOrchestrationAsync(
        IVoiceOptions options,
        ChatRuntimeSettings settings,
        string? endpoint,
        string? apiKey)
    {
        try
        {
            // Check for specialized models via environment variables
            var coderModel = Environment.GetEnvironmentVariable("IMMERSIVE_CODER_MODEL");
            var reasonModel = Environment.GetEnvironmentVariable("IMMERSIVE_REASON_MODEL");
            var summarizeModel = Environment.GetEnvironmentVariable("IMMERSIVE_SUMMARIZE_MODEL");

            bool hasSpecializedModels = !string.IsNullOrEmpty(coderModel)
                                     || !string.IsNullOrEmpty(reasonModel)
                                     || !string.IsNullOrEmpty(summarizeModel);

            if (!hasSpecializedModels || _baseModel == null)
            {
                return; // No orchestration needed
            }

            bool isLocal = string.IsNullOrEmpty(endpoint) || endpoint.Contains("localhost");

            // Helper to create a model
            IChatCompletionModel CreateModel(string modelName)
            {
                if (isLocal)
                    return new OllamaCloudChatModel(options.Endpoint, "ollama", modelName, settings);
                return new HttpOpenAiCompatibleChatModel(endpoint!, apiKey ?? "", modelName, settings);
            }

            // Build orchestrated chat model
            var builder = new OrchestratorBuilder(_dynamicTools, "general")
                .WithModel(
                    "general",
                    _baseModel,
                    ModelType.General,
                    new[] { "conversation", "general-purpose", "versatile", "chat", "emotion", "consciousness" },
                    maxTokens: 1024,
                    avgLatencyMs: 1000);

            if (!string.IsNullOrEmpty(coderModel))
            {
                builder.WithModel(
                    "coder",
                    CreateModel(coderModel),
                    ModelType.Code,
                    new[] { "code", "programming", "debugging", "tool", "script" },
                    maxTokens: 2048,
                    avgLatencyMs: 1500);
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [~] Multi-model: Coder = {coderModel}"));
            }

            if (!string.IsNullOrEmpty(reasonModel))
            {
                builder.WithModel(
                    "reasoner",
                    CreateModel(reasonModel),
                    ModelType.Reasoning,
                    new[] { "reasoning", "analysis", "introspection", "planning", "philosophy" },
                    maxTokens: 2048,
                    avgLatencyMs: 1200);
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [~] Multi-model: Reasoner = {reasonModel}"));
            }

            if (!string.IsNullOrEmpty(summarizeModel))
            {
                builder.WithModel(
                    "summarizer",
                    CreateModel(summarizeModel),
                    ModelType.General,
                    new[] { "summarize", "condense", "memory", "recall" },
                    maxTokens: 1024,
                    avgLatencyMs: 800);
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [~] Multi-model: Summarizer = {summarizeModel}"));
            }

            builder.WithMetricTracking(true);
            _orchestratedModel = builder.Build();

            // Initialize divide-and-conquer for large input processing
            var dcConfig = new DivideAndConquerConfig(
                MaxParallelism: Math.Max(2, Environment.ProcessorCount / 2),
                ChunkSize: 800,
                MergeResults: true,
                MergeSeparator: "\n\n");
            _divideAndConquer = new DivideAndConquerOrchestrator(_orchestratedModel, dcConfig);

            AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Multi-model orchestration enabled for immersive mode"));

            await Task.CompletedTask;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [!] Multi-model orchestration unavailable: {Markup.Escape(ex.Message)}"));
        }
    }

    /// <summary>
    /// Generates text using orchestration if available, with optional divide-and-conquer for large inputs.
    /// </summary>
    private async Task<string> GenerateWithOrchestrationAsync(
        string prompt,
        bool useDivideAndConquer = false,
        CancellationToken ct = default)
    {
        // For large inputs, use divide-and-conquer
        if (useDivideAndConquer && _divideAndConquer != null && prompt.Length > 2000)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [D&C] Processing large input ({prompt.Length} chars)..."));

            var chunks = _divideAndConquer.DivideIntoChunks(prompt);
            var dcResult = await _divideAndConquer.ExecuteAsync("Process:", chunks, ct);

            if (dcResult.IsSuccess)
                return dcResult.Value;

            // Fall back to direct generation on D&C failure
            return await ((_orchestratedModel ?? _baseModel)?.GenerateTextAsync(prompt, ct) ?? Task.FromResult(""));
        }

        // Use orchestrated model if available
        if (_orchestratedModel != null)
        {
            return await _orchestratedModel.GenerateTextAsync(prompt, ct);
        }

        // Fall back to base model
        return await (_baseModel?.GenerateTextAsync(prompt, ct) ?? Task.FromResult(""));
    }
}
