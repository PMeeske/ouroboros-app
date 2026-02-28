using System.Diagnostics;
using LangChain.Providers.Ollama;
using Microsoft.Extensions.Logging;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Application.Configuration;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Abstractions.Monads;
using Spectre.Console;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Service implementation for MeTTa orchestrator operations.
/// Extracted from the static <c>MeTTaCommands</c> class to follow the
/// handler → service pattern used by all other CLI commands.
/// </summary>
public sealed class MeTTaService : IMeTTaService
{
    private readonly ISpectreConsoleService _console;
    private readonly ILogger<MeTTaService> _logger;

    public MeTTaService(
        ISpectreConsoleService console,
        ILogger<MeTTaService> logger)
    {
        _console = console;
        _logger = logger;
    }

    public async Task RunAsync(MeTTaConfig config, CancellationToken cancellationToken = default)
    {
        // Interactive mode
        if (config.Interactive)
        {
            await MeTTaInteractiveMode.RunInteractiveAsync();
            return;
        }

        // Voice mode integration
        if (config.Voice)
        {
            await RunVoiceModeAsync(config);
            return;
        }

        await RunStandardModeAsync(config);
    }

    private async Task RunStandardModeAsync(MeTTaConfig config)
    {
        _console.MarkupLine("[bold cyan]╔════════════════════════════════════════════════════════════╗[/]");
        _console.MarkupLine("[bold cyan]║   MeTTa Orchestrator v3.0 - Symbolic Reasoning            ║[/]");
        _console.MarkupLine("[bold cyan]╚════════════════════════════════════════════════════════════╝[/]");
        _console.WriteLine();

        if (config.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");

        try
        {
            var (chatModel, embedModel) = CreateModels(config);

            _console.MarkupLine("[green]✓[/] Initializing MeTTa orchestrator...");
            var orchestratorBuilder = MeTTaOrchestratorBuilder.CreateDefault(embedModel)
                .WithLLM(chatModel);

            var orchestrator = orchestratorBuilder.Build();
            _console.MarkupLine("[green]✓[/] MeTTa orchestrator v3.0 initialized");
            _console.WriteLine();

            _console.MarkupLine($"[bold]Goal:[/] {config.Goal}");
            _console.WriteLine();

            _console.MarkupLine("[bold]=== Planning Phase ===[/]");
            var sw = Stopwatch.StartNew();
            var planResult = await orchestrator.PlanAsync(config.Goal);

            var plan = planResult.Match(
                success => success,
                error =>
                {
                    _console.MarkupLine($"[red]Planning failed:[/] {error}");
                    return null!;
                });

            if (plan == null) return;

            sw.Stop();
            _console.MarkupLine($"[green]✓[/] Plan generated in {sw.ElapsedMilliseconds}ms");
            _console.MarkupLine($"  Steps: {plan.Steps.Count}");
            _console.MarkupLine($"  Overall confidence: {plan.ConfidenceScores.GetValueOrDefault("overall", 0):P2}");
            _console.WriteLine();

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                var step = plan.Steps[i];
                _console.MarkupLine($"  {i + 1}. {Markup.Escape(step.Action)}");
                _console.MarkupLine($"     Expected: {Markup.Escape(step.ExpectedOutcome)}");
                _console.MarkupLine($"     Confidence: {step.ConfidenceScore:P2}");
            }
            _console.WriteLine();

            if (config.PlanOnly)
            {
                _console.MarkupLine("[green]✓[/] Plan-only mode - skipping execution");
                return;
            }

            _console.MarkupLine("[bold]=== Execution Phase ===[/]");
            sw.Restart();
            var executionResult = await orchestrator.ExecuteAsync(plan);
            sw.Stop();

            executionResult.Match(
                success =>
                {
                    _console.MarkupLine($"[green]✓[/] Execution completed in {sw.ElapsedMilliseconds}ms");
                    _console.MarkupLine($"\n[bold]Final Result:[/]");
                    _console.MarkupLine($"  Success: {success.Success}");
                    _console.MarkupLine($"  Duration: {success.Duration.TotalSeconds:F2}s");
                    if (!string.IsNullOrWhiteSpace(success.FinalOutput))
                    {
                        _console.MarkupLine($"  Output: {Markup.Escape(success.FinalOutput)}");
                    }
                    _console.MarkupLine($"\n[bold]Step Results:[/]");
                    for (int i = 0; i < success.StepResults.Count; i++)
                    {
                        var stepResult = success.StepResults[i];
                        _console.MarkupLine($"  {i + 1}. {Markup.Escape(stepResult.Step.Action)}");
                        _console.MarkupLine($"     Success: {stepResult.Success}");
                        _console.MarkupLine($"     Output: {Markup.Escape(stepResult.Output)}");
                        if (!string.IsNullOrEmpty(stepResult.Error))
                        {
                            _console.MarkupLine($"     [red]Error:[/] {Markup.Escape(stepResult.Error)}");
                        }
                    }
                },
                error =>
                {
                    _console.MarkupLine($"[red]Execution failed:[/] {Markup.Escape(error)}");
                });

            if (config.ShowMetrics)
            {
                _console.MarkupLine("\n[bold]=== Performance Metrics ===[/]");
                var metrics = orchestrator.GetMetrics();

                foreach (var (key, metric) in metrics)
                {
                    _console.MarkupLine($"\n[bold]{Markup.Escape(key)}:[/]");
                    _console.MarkupLine($"  Executions: {metric.ExecutionCount}");
                    _console.MarkupLine($"  Avg Latency: {metric.AverageLatencyMs:F2}ms");
                    _console.MarkupLine($"  Success Rate: {metric.SuccessRate:P2}");
                    _console.MarkupLine($"  Last Used: {metric.LastUsed:g}");
                }
            }

            _console.MarkupLine("\n[green]✓[/] MeTTa orchestrator execution completed successfully");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("ECONNREFUSED"))
        {
            _console.MarkupLine("[red]Error:[/] Ollama is not running. Please start Ollama before using the MeTTa orchestrator.");
            _console.MarkupLine("[dim]  Run: ollama serve[/]");
        }
        catch (Exception ex) when (ex.Message.Contains("metta") && (ex.Message.Contains("not found") || ex.Message.Contains("No such file")))
        {
            _console.MarkupLine("[red]Error:[/] MeTTa engine not found. Please install MeTTa:");
            _console.MarkupLine("[dim]  Install from: https://github.com/trueagi-io/hyperon-experimental[/]");
            _console.MarkupLine("[dim]  Ensure 'metta' executable is in your PATH[/]");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "MeTTa orchestrator failed");
            _console.MarkupLine($"[red]MeTTa Orchestrator Failed:[/] {Markup.Escape(ex.Message)}");
            if (config.Debug)
            {
                _console.MarkupLine($"[dim]{Markup.Escape(ex.StackTrace ?? "")}[/]");
            }
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            _logger.LogError(ex, "MeTTa orchestrator failed");
            _console.MarkupLine($"[red]MeTTa Orchestrator Failed:[/] {Markup.Escape(ex.Message)}");
            if (config.Debug)
            {
                _console.MarkupLine($"[dim]{Markup.Escape(ex.StackTrace ?? "")}[/]");
            }
        }
    }

    private static async Task RunVoiceModeAsync(MeTTaConfig config)
    {
        var voiceService = VoiceModeExtensions.CreateVoiceService(
            voice: true,
            persona: config.Persona,
            voiceOnly: config.VoiceOnly,
            localTts: config.LocalTts,
            voiceLoop: config.VoiceLoop,
            model: config.Model,
            endpoint: config.Endpoint ?? DefaultEndpoints.Ollama);

        await voiceService.InitializeAsync();
        voiceService.PrintHeader("METTA ORCHESTRATOR");

        var (chatModel, embedModel) = CreateModels(config);
        var orchestrator = MeTTaOrchestratorBuilder.CreateDefault(embedModel).WithLLM(chatModel).Build();

        await voiceService.SayAsync("MeTTa orchestrator is ready! Give me a goal and I'll plan and execute it using symbolic reasoning.");

        // Initial goal if provided
        if (!string.IsNullOrWhiteSpace(config.Goal) && !config.Goal.Equals("interactive", StringComparison.OrdinalIgnoreCase))
        {
            await ProcessGoalWithVoiceAsync(orchestrator, config.Goal, config.PlanOnly, voiceService);
            if (!config.VoiceLoop)
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
                await voiceService.SayAsync("Goodbye! The symbolic reasoning engine awaits your return.");
                running = false;
                continue;
            }

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                await voiceService.SayAsync("Tell me a goal and I'll create a plan using MeTTa symbolic reasoning, then execute it step by step. Say 'plan only' before your goal to skip execution.");
                continue;
            }

            bool planOnly = input.StartsWith("plan only", StringComparison.OrdinalIgnoreCase) ||
                           input.StartsWith("just plan", StringComparison.OrdinalIgnoreCase);
            string goal = planOnly ? input.Replace("plan only", "", StringComparison.OrdinalIgnoreCase)
                                         .Replace("just plan", "", StringComparison.OrdinalIgnoreCase).Trim() : input;

            await ProcessGoalWithVoiceAsync(orchestrator, goal, planOnly, voiceService);
        }

        voiceService.Dispose();
    }

    private static async Task ProcessGoalWithVoiceAsync(
        MeTTaOrchestrator orchestrator,
        string goal,
        bool planOnly,
        VoiceModeService voiceService)
    {
        try
        {
            await voiceService.SayAsync($"Planning for: {goal}");

            var planResult = await orchestrator.PlanAsync(goal);
            string? planError = null;
            var plan = planResult.Match<Plan?>(
                success => success,
                error => { planError = error; return null; });
            if (planError != null) await voiceService.SayAsync($"Planning failed: {planError}");

            if (plan == null) return;

            var steps = string.Join(", ", plan.Steps.Take(3).Select(s => s.Action));
            await voiceService.SayAsync($"I've created a plan with {plan.Steps.Count} steps: {steps}. Overall confidence is {plan.ConfidenceScores.GetValueOrDefault("overall", 0):P0}.");

            if (planOnly)
            {
                await voiceService.SayAsync("Plan only mode - skipping execution as requested.");
                return;
            }

            await voiceService.SayAsync("Executing the plan now...");
            var executionResult = await orchestrator.ExecuteAsync(plan);

            string? execSummary = null;
            string? execError = null;
            executionResult.Match(
                success =>
                {
                    execSummary = success.Success
                        ? $"Execution completed successfully in {success.Duration.TotalSeconds:F1} seconds."
                        : "Execution completed with some issues.";
                    if (!string.IsNullOrWhiteSpace(success.FinalOutput))
                    {
                        execSummary += $" Result: {success.FinalOutput}";
                    }
                },
                error => { execError = error; });
            if (execSummary != null) await voiceService.SayAsync(execSummary);
            if (execError != null) await voiceService.SayAsync($"Execution failed: {execError}");
        }
        catch (InvalidOperationException ex)
        {
            await voiceService.SayAsync($"Something went wrong: {ex.Message}");
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            await voiceService.SayAsync($"Something went wrong: {ex.Message}");
        }
    }

    private static (IChatCompletionModel chatModel, IEmbeddingModel embedModel) CreateModels(MeTTaConfig config)
    {
        var provider = new OllamaProvider();
        var settings = new ChatRuntimeSettings(config.Temperature, config.MaxTokens, config.TimeoutSeconds, false, config.Culture);

        var (endpoint, apiKey, endpointType) = ChatConfig.ResolveWithOverrides(
            config.Endpoint,
            config.ApiKey,
            config.EndpointType);

        IChatCompletionModel chatModel;
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            chatModel = ServiceFactory.CreateRemoteChatModel(endpoint, apiKey, config.Model, settings, endpointType);
        }
        else
        {
            chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, config.Model), config.Culture);
        }

        var embedModel = ServiceFactory.CreateEmbeddingModel(endpoint, apiKey, endpointType, config.Embed, provider);

        return (chatModel, embedModel);
    }
}
