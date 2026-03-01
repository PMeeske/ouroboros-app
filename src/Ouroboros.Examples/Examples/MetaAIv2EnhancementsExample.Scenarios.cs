// <copyright file="MetaAIv2EnhancementsExample.Scenarios.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using LangChain.Providers.Ollama;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;

/// <summary>
/// Advanced scenarios: adaptive planning, cost-aware routing, human-in-the-loop,
/// runner, and helper methods.
/// </summary>
public static partial class MetaAIv2EnhancementsExample
{
    /// <summary>
    /// Demonstrates adaptive planning with real-time adjustments.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateAdaptivePlanning()
    {
        Console.WriteLine("=== Adaptive Planning Example ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        AdaptivePlanner adaptivePlanner = new AdaptivePlanner(orchestrator, chatModel);

        // Create a plan with varying confidence
        Plan plan = new Plan(
            "Process data with error handling",
            new List<PlanStep>
            {
                new PlanStep("validate_input", new Dictionary<string, object>(), "validated", 0.9),
                new PlanStep("risky_operation", new Dictionary<string, object>(), "processed", 0.3), // Low confidence
                new PlanStep("finalize", new Dictionary<string, object>(), "final", 0.9),
            },
            new Dictionary<string, double> { ["overall"] = 0.7 },
            DateTime.UtcNow);

        AdaptivePlanningConfig config = new AdaptivePlanningConfig(
            MaxRetries: 2,
            EnableAutoReplan: false,
            FailureThreshold: 0.5);

        Result<PlanExecutionResult, string> result = await adaptivePlanner.ExecuteWithAdaptationAsync(plan, config);

        if (result.IsSuccess)
        {
            PlanExecutionResult execution = result.Value;
            Console.WriteLine($"Adaptive execution completed:");
            Console.WriteLine($"  Success: {execution.Success}");

            if (execution.Metadata.TryGetValue("adaptations", out object? adaptations))
            {
                List<string> adaptList = (List<string>)adaptations;
                Console.WriteLine($"  Adaptations made: {adaptList.Count}");
                foreach (string adaptation in adaptList)
                {
                    Console.WriteLine($"    - {adaptation}");
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates cost-aware routing.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateCostAwareRouting()
    {
        Console.WriteLine("=== Cost-Aware Routing Example ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        SmartModelOrchestrator baseOrchestrator = new SmartModelOrchestrator(tools, "default");

        // Register a real model
        baseOrchestrator.RegisterModel(
            new ModelCapability("llama3", new[] { "general" }, 2048, 1.0, 500, ModelType.General),
            chatModel);

        UncertaintyRouter uncertaintyRouter = new UncertaintyRouter(baseOrchestrator, 0.7);

        MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        CostAwareRouter costRouter = new CostAwareRouter(uncertaintyRouter, orchestrator);

        // Try different optimization strategies
        CostOptimizationStrategy[] strategies = new[]
        {
            CostOptimizationStrategy.MinimizeCost,
            CostOptimizationStrategy.MaximizeQuality,
            CostOptimizationStrategy.Balanced,
            CostOptimizationStrategy.MaximizeValue,
        };

        foreach (CostOptimizationStrategy strategy in strategies)
        {
            CostAwareRoutingConfig config = new CostAwareRoutingConfig(
                MaxCostPerPlan: 0.5,
                MinAcceptableQuality: 0.7,
                Strategy: strategy);

            Result<CostBenefitAnalysis, string> result = await costRouter.RouteWithCostAwarenessAsync(
                "Process complex data analysis",
                null,
                config);

            if (result.IsSuccess)
            {
                CostBenefitAnalysis analysis = result.Value;
                Console.WriteLine($"Strategy: {strategy}");
                Console.WriteLine($"  Route: {analysis.RecommendedRoute}");
                Console.WriteLine($"  Cost: ${analysis.EstimatedCost:F6}");
                Console.WriteLine($"  Quality: {analysis.EstimatedQuality:P0}");
                Console.WriteLine($"  Value: {analysis.ValueScore:F3}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates human-in-the-loop workflows.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateHumanInTheLoop()
    {
        Console.WriteLine("=== Human-in-the-Loop Example ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        // Use auto-approving mock for demonstration
        AutoApprovingFeedbackProvider mockProvider = new AutoApprovingFeedbackProvider();
        HumanInTheLoopOrchestrator hitlOrchestrator = new HumanInTheLoopOrchestrator(orchestrator, mockProvider);

        Plan plan = new Plan(
            "Database maintenance",
            new List<PlanStep>
            {
                new PlanStep("backup_database", new Dictionary<string, object>(), "backup created", 0.95),
                new PlanStep("delete_old_records", new Dictionary<string, object>(), "records deleted", 0.9), // Critical
                new PlanStep("optimize_tables", new Dictionary<string, object>(), "tables optimized", 0.9),
            },
            new Dictionary<string, double> { ["overall"] = 0.91 },
            DateTime.UtcNow);

        HumanInTheLoopConfig config = new HumanInTheLoopConfig(
            RequireApprovalForCriticalSteps: true,
            EnableInteractiveRefinement: false,
            DefaultTimeout: TimeSpan.FromMinutes(2),
            CriticalActionPatterns: new List<string> { "delete", "drop", "remove" });

        Result<PlanExecutionResult, string> result = await hitlOrchestrator.ExecuteWithHumanOversightAsync(plan, config);

        if (result.IsSuccess)
        {
            PlanExecutionResult execution = result.Value;
            Console.WriteLine($"Human oversight execution:");
            Console.WriteLine($"  Success: {execution.Success}");

            if (execution.Metadata.TryGetValue("approvals", out object? approvals))
            {
                List<string> approvalList = (List<string>)approvals;
                Console.WriteLine($"  Approval events: {approvalList.Count}");
                foreach (string approval in approvalList)
                {
                    Console.WriteLine($"    - {approval}");
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Runs all enhancement demonstrations.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllDemonstrations()
    {
        Console.WriteLine("\n" + new string('=', 70));
        Console.WriteLine("META-AI V2 ENHANCEMENTS DEMONSTRATION");
        Console.WriteLine(new string('=', 70) + "\n");

        try
        {
            await DemonstrateParallelExecution();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"Parallel execution demo skipped: {ex.Message}\n");
        }

        try
        {
            await DemonstrateHierarchicalPlanning();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"Hierarchical planning demo skipped: {ex.Message}\n");
        }

        try
        {
            await DemonstrateExperienceReplay();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"Experience replay demo skipped: {ex.Message}\n");
        }

        await DemonstrateSkillComposition();
        await DemonstrateDistributedOrchestration();

        try
        {
            await DemonstrateAdaptivePlanning();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"Adaptive planning demo skipped: {ex.Message}\n");
        }

        try
        {
            await DemonstrateCostAwareRouting();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"Cost-aware routing demo skipped: {ex.Message}\n");
        }

        try
        {
            await DemonstrateHumanInTheLoop();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"Human-in-the-loop demo skipped: {ex.Message}\n");
        }

        Console.WriteLine(new string('=', 70));
        Console.WriteLine("DEMONSTRATIONS COMPLETED");
        Console.WriteLine(new string('=', 70) + "\n");
    }

    // Helper methods
    private static Experience CreateSampleExperience(string goal, double quality)
    {
        Plan plan = new Plan(
            goal,
            new List<PlanStep> { CreateSampleStep("action") },
            new Dictionary<string, double> { ["overall"] = quality },
            DateTime.UtcNow);

        PlanExecutionResult execution = new PlanExecutionResult(
            plan,
            new List<StepResult>
            {
                new StepResult(
                    plan.Steps[0],
                    true,
                    "Success",
                    null,
                    TimeSpan.FromSeconds(1),
                    new Dictionary<string, object>()),
            },
            true,
            "Success",
            new Dictionary<string, object>(),
            TimeSpan.FromSeconds(1));

        PlanVerificationResult verification = new PlanVerificationResult(
            execution,
            true,
            quality,
            new List<string>(),
            new List<string>(),
            null);

        return ExperienceFactory.FromExecution(goal, execution, verification);
    }

    private static PlanStep CreateSampleStep(string action)
    {
        return new PlanStep(
            action,
            new Dictionary<string, object> { ["param"] = "value" },
            $"{action} result",
            0.85);
    }

    private class AutoApprovingFeedbackProvider : IHumanFeedbackProvider
    {
        public Task<HumanFeedbackResponse> RequestFeedbackAsync(
            HumanFeedbackRequest request,
            CancellationToken ct = default)
        {
            return Task.FromResult(new HumanFeedbackResponse(
                request.RequestId,
                "approve",
                null,
                DateTime.UtcNow));
        }

        public Task<ApprovalResponse> RequestApprovalAsync(
            ApprovalRequest request,
            CancellationToken ct = default)
        {
            Console.WriteLine($"  [Auto-approved] {request.Action}");
            return Task.FromResult(new ApprovalResponse(
                request.RequestId,
                true,
                "Auto-approved for demo",
                null,
                DateTime.UtcNow));
        }
    }
}
