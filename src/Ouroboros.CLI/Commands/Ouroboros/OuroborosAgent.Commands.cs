// <copyright file="OuroborosAgent.Commands.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using MediatR;
using Ouroboros.CLI.Mediator;
using Ouroboros.Abstractions.Monads;

namespace Ouroboros.CLI.Commands;

public sealed partial class OuroborosAgent
{
    private Task<string> PlanAsync(string goal)
        => _mediator.Send(new PlanRequest(goal));

    private Task<string> ExecuteAsync(string goal)
        => _mediator.Send(new ExecuteGoalRequest(goal));

    private Task<string> FetchResearchAsync(string query)
        => _mediator.Send(new FetchResearchRequest(query));

    private Task<string> ProcessLargeInputAsync(string input)
        => _mediator.Send(new ProcessLargeTextRequest(input));

    private Task<string> RememberAsync(string info)
        => _mediator.Send(new RememberRequest(info));

    private Task<string> RecallAsync(string topic)
        => _mediator.Send(new RecallRequest(topic));

    private async Task<string> QueryMeTTaAsync(string query)
    {
        var result = await QueryMeTTaResultAsync(query);
        return result.Match(
            success => $"MeTTa result: {success}",
            error => $"Query error: {error}");
    }

    private Task<Result<string, string>> QueryMeTTaResultAsync(string query)
        => _mediator.Send(new QueryMeTTaRequest(query));

    // ================================================================
    // UNIFIED CLI COMMANDS - All Ouroboros capabilities in one place
    // ================================================================

    /// <summary>
    /// Ask a single question (routes to AskCommands CLI handler).
    /// </summary>
    private async Task<string> AskAsync(string question)
    {
        var result = await AskResultAsync(question);
        return result.Match(success => success, error => $"Error asking question: {error}");
    }

    private Task<Result<string, string>> AskResultAsync(string question)
        => _mediator.Send(new AskResultRequest(question));

    // IAgentFacade explicit implementations for monadic operations
    Task<Result<string, string>> IAgentFacade.AskResultAsync(string question) => AskResultAsync(question);
    Task<Result<string, string>> IAgentFacade.RunPipelineResultAsync(string dsl) => RunPipelineResultAsync(dsl);
    Task<Result<string, string>> IAgentFacade.RunMeTTaExpressionResultAsync(string expression) => RunMeTTaExpressionResultAsync(expression);
    Task<Result<string, string>> IAgentFacade.QueryMeTTaResultAsync(string query) => QueryMeTTaResultAsync(query);

    private async Task<string> RunPipelineAsync(string dsl)
    {
        var result = await RunPipelineResultAsync(dsl);
        return result.Match(success => success, error => $"Pipeline error: {error}");
    }

    private Task<Result<string, string>> RunPipelineResultAsync(string dsl)
        => _mediator.Send(new RunPipelineResultRequest(dsl));

    /// <summary>
    /// Execute a MeTTa expression directly (routes to IMeTTaService).
    /// </summary>
    private async Task<string> RunMeTTaExpressionAsync(string expression)
    {
        var result = await RunMeTTaExpressionResultAsync(expression);
        return result.Match(success => success, error => $"MeTTa execution failed: {error}");
    }

    private Task<Result<string, string>> RunMeTTaExpressionResultAsync(string expression)
        => _mediator.Send(new RunMeTTaExpressionResultRequest(expression));

    private Task<string> OrchestrateAsync(string goal)
        => _mediator.Send(new OrchestrateRequest(goal));

    private Task<string> NetworkCommandAsync(string subCommand)
        => _mediator.Send(new NetworkCommandRequest(subCommand));

    private Task<string> DagCommandAsync(string subCommand)
        => _mediator.Send(new DagCommandRequest(subCommand));

    private Task<string> AffectCommandAsync(string subCommand)
        => _mediator.Send(new AffectCommandRequest(subCommand));

    private Task<string> EnvironmentCommandAsync(string subCommand)
        => _mediator.Send(new EnvironmentCommandRequest(subCommand));

    private Task<string> MaintenanceCommandAsync(string subCommand)
        => _mediator.Send(new MaintenanceCommandRequest(subCommand));

    private Task<string> PolicyCommandAsync(string subCommand)
        => _mediator.Send(new PolicyCommandRequest(subCommand));

    private Task<string> SwarmCommandAsync(string argument)
        => Swarm.SwarmCommandHandler.HandleAsync(argument, _swarmSub);

    private Task<string> RunTestAsync(string testSpec)
        => _mediator.Send(new RunTestRequest(testSpec));

    /// <summary>Runs the full LLM chat pipeline (delegated to ChatSubsystem).</summary>
    private Task<string> ChatAsync(string input)
        => _mediator.Send(new ChatRequest(input));

    private static bool IsExitCommand(string input)
    {
        var exitWords = new[] { "exit", "quit", "goodbye", "bye", "later", "see you", "q!", "stop" };
        return exitWords.Any(w => input.Equals(w, StringComparison.OrdinalIgnoreCase) ||
                                  input.StartsWith(w + " ", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a Tapo device type is a camera (for RTSP streaming).
    /// </summary>
    private static bool IsCameraDeviceType(Ouroboros.Providers.Tapo.TapoDeviceType deviceType) =>
        deviceType is Ouroboros.Providers.Tapo.TapoDeviceType.C100
            or Ouroboros.Providers.Tapo.TapoDeviceType.C200
            or Ouroboros.Providers.Tapo.TapoDeviceType.C210
            or Ouroboros.Providers.Tapo.TapoDeviceType.C220
            or Ouroboros.Providers.Tapo.TapoDeviceType.C310
            or Ouroboros.Providers.Tapo.TapoDeviceType.C320
            or Ouroboros.Providers.Tapo.TapoDeviceType.C420
            or Ouroboros.Providers.Tapo.TapoDeviceType.C500
            or Ouroboros.Providers.Tapo.TapoDeviceType.C520;

    public Task ProcessGoalAsync(string goal)
        => _mediator.Send(new ProcessGoalRequest(goal));

    /// <summary>
    /// Processes an initial question provided via command line.
    /// </summary>
    public Task ProcessQuestionAsync(string question)
        => _mediator.Send(new ProcessQuestionRequest(question));

    /// <summary>
    /// Processes and executes a pipeline DSL string.
    /// </summary>
    public Task ProcessDslAsync(string dsl)
        => _mediator.Send(new ProcessDslRequest(dsl));

    // ═══════════════════════════════════════════════════════════════════════════
    // MULTI-MODEL ORCHESTRATION & DIVIDE-AND-CONQUER HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates text using multi-model orchestration if available, falling back to single model.
    /// The orchestrator automatically routes to specialized models (coder, reasoner, summarizer)
    /// based on prompt content analysis.
}