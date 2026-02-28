// <copyright file="MetaAIv2EnhancementsExample.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using LangChain.Providers.Ollama;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;

/// <summary>
/// Comprehensive example demonstrating Meta-AI v2 enhancements.
/// </summary>
public static partial class MetaAIv2EnhancementsExample
{
    /// <summary>
    /// Demonstrates parallel execution of independent steps.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateParallelExecution()
    {
        Console.WriteLine("=== Parallel Execution Example ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        // Create a plan with independent steps
        Result<Plan, string> planResult = await orchestrator.PlanAsync(
            "Analyze three different datasets concurrently",
            new Dictionary<string, object>
            {
                ["datasets"] = new[] { "sales.csv", "inventory.csv", "customers.csv" },
            });

        if (planResult.IsSuccess)
        {
            Plan plan = planResult.Value;
            Console.WriteLine($"Plan created with {plan.Steps.Count} steps");

            // Execute with automatic parallel detection
            Result<PlanExecutionResult, string> execResult = await orchestrator.ExecuteAsync(plan);

            if (execResult.IsSuccess)
            {
                PlanExecutionResult execution = execResult.Value;
                object isParallel = execution.Metadata.GetValueOrDefault("parallel_execution", false);
                object speedup = execution.Metadata.GetValueOrDefault("estimated_speedup", 1.0);

                Console.WriteLine($"Parallel execution: {isParallel}");
                Console.WriteLine($"Estimated speedup: {speedup:F2}x");
                Console.WriteLine($"Duration: {execution.Duration.TotalMilliseconds:F0}ms");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates hierarchical planning for complex tasks.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateHierarchicalPlanning()
    {
        Console.WriteLine("=== Hierarchical Planning Example ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        HierarchicalPlanner hierarchicalPlanner = new HierarchicalPlanner(orchestrator, chatModel);

        HierarchicalPlanningConfig config = new HierarchicalPlanningConfig(
            MaxDepth: 3,
            MinStepsForDecomposition: 3,
            ComplexityThreshold: 0.6);

        Result<HierarchicalPlan, string> result = await hierarchicalPlanner.CreateHierarchicalPlanAsync(
            "Design and implement a complete REST API with database",
            null,
            config);

        if (result.IsSuccess)
        {
            HierarchicalPlan plan = result.Value;
            Console.WriteLine($"Hierarchical plan created:");
            Console.WriteLine($"  Top-level steps: {plan.TopLevelPlan.Steps.Count}");
            Console.WriteLine($"  Sub-plans: {plan.SubPlans.Count}");
            Console.WriteLine($"  Max depth: {plan.MaxDepth}");

            foreach ((string stepName, Plan subPlan) in plan.SubPlans.Take(3))
            {
                Console.WriteLine($"  Sub-plan '{stepName}': {subPlan.Steps.Count} steps");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates experience replay for continual learning.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateExperienceReplay()
    {
        Console.WriteLine("=== Experience Replay Example ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        MemoryStore memory = new MemoryStore();
        SkillRegistry skills = new SkillRegistry();

        ExperienceReplay replay = new ExperienceReplay(memory, skills, chatModel);

        // Simulate storing some experiences
        Console.WriteLine("Storing experiences...");
        for (int i = 0; i < 5; i++)
        {
            Experience experience = CreateSampleExperience($"Task {i + 1}", 0.75 + (i * 0.04));
            await memory.StoreExperienceAsync(experience);
        }

        ExperienceReplayConfig config = new ExperienceReplayConfig(
            BatchSize: 3,
            MinQualityScore: 0.75,
            PrioritizeHighQuality: true);

        Result<TrainingResult, string> result = await replay.TrainOnExperiencesAsync(config);

        if (result.IsSuccess)
        {
            TrainingResult training = result.Value;
            Console.WriteLine($"Training completed:");
            Console.WriteLine($"  Experiences processed: {training.ExperiencesProcessed}");
            Console.WriteLine($"  Patterns discovered: {training.ImprovedMetrics.GetValueOrDefault("patterns_discovered", 0)}");
            Console.WriteLine($"  Skills extracted: {training.ImprovedMetrics.GetValueOrDefault("skills_extracted", 0)}");
            Console.WriteLine($"  Average quality: {training.ImprovedMetrics.GetValueOrDefault("avg_quality", 0):P0}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates skill composition.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateSkillComposition()
    {
        Console.WriteLine("=== Skill Composition Example ===\n");

        SkillRegistry skills = new SkillRegistry();
        MemoryStore memory = new MemoryStore();
        SkillComposer composer = new SkillComposer(skills, memory);

        // Register base skills
        Skill extractSkill = new Skill(
            "extract_data",
            "Extract data from source",
            new List<string>(),
            new List<PlanStep> { CreateSampleStep("extract") },
            0.9,
            10,
            DateTime.UtcNow,
            DateTime.UtcNow);

        Skill transformSkill = new Skill(
            "transform_data",
            "Transform and clean data",
            new List<string>(),
            new List<PlanStep> { CreateSampleStep("transform") },
            0.85,
            8,
            DateTime.UtcNow,
            DateTime.UtcNow);

        Skill loadSkill = new Skill(
            "load_data",
            "Load data to destination",
            new List<string>(),
            new List<PlanStep> { CreateSampleStep("load") },
            0.88,
            12,
            DateTime.UtcNow,
            DateTime.UtcNow);

        skills.RegisterSkill(extractSkill.ToAgentSkill());
        skills.RegisterSkill(transformSkill.ToAgentSkill());
        skills.RegisterSkill(loadSkill.ToAgentSkill());

        // Compose into ETL pipeline skill
        Result<Skill, string> compositeResult = await composer.ComposeSkillsAsync(
            "etl_pipeline",
            "Complete ETL data pipeline",
            new List<string> { "extract_data", "transform_data", "load_data" });

        if (compositeResult.IsSuccess)
        {
            Skill composite = compositeResult.Value;
            Console.WriteLine($"Composite skill created: {composite.Name}");
            Console.WriteLine($"  Description: {composite.Description}");
            Console.WriteLine($"  Total steps: {composite.Steps.Count}");
            Console.WriteLine($"  Success rate: {composite.SuccessRate:P0}");

            // Demonstrate decomposition
            Result<List<Skill>, string> decomposeResult = composer.DecomposeSkill("etl_pipeline");
            if (decomposeResult.IsSuccess)
            {
                Console.WriteLine($"  Components: {string.Join(", ", decomposeResult.Value.Select(s => s.Name))}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates distributed orchestration.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateDistributedOrchestration()
    {
        Console.WriteLine("=== Distributed Orchestration Example ===\n");

        SafetyGuard safety = new SafetyGuard(PermissionLevel.Isolated);
        DistributedOrchestrator orchestrator = new DistributedOrchestrator(safety);

        // Register agents with different capabilities
        AgentInfo[] agents = new[]
        {
            new AgentInfo("compute-1", "Compute Agent 1", new HashSet<string> { "compute", "analysis" }, AgentStatus.Available, DateTime.UtcNow),
            new AgentInfo("storage-1", "Storage Agent", new HashSet<string> { "storage", "database" }, AgentStatus.Available, DateTime.UtcNow),
            new AgentInfo("compute-2", "Compute Agent 2", new HashSet<string> { "compute", "ml" }, AgentStatus.Available, DateTime.UtcNow),
        };

        foreach (AgentInfo? agent in agents)
        {
            orchestrator.RegisterAgent(agent);
        }

        Console.WriteLine($"Registered {agents.Length} agents");

        // Create a plan for distributed execution
        Plan plan = new Plan(
            "Distributed data processing",
            new List<PlanStep>
            {
                new PlanStep("compute", new Dictionary<string, object> { ["task"] = "analyze" }, "analysis result", 0.9),
                new PlanStep("storage", new Dictionary<string, object> { ["task"] = "store" }, "storage result", 0.9),
                new PlanStep("ml", new Dictionary<string, object> { ["task"] = "predict" }, "predictions", 0.9),
            },
            new Dictionary<string, double> { ["overall"] = 0.9 },
            DateTime.UtcNow);

        Result<PlanExecutionResult, string> result = await orchestrator.ExecuteDistributedAsync(plan);

        if (result.IsSuccess)
        {
            PlanExecutionResult execution = result.Value;
            Console.WriteLine($"Distributed execution completed:");
            Console.WriteLine($"  Agents used: {execution.Metadata.GetValueOrDefault("agents_used", 0)}");
            Console.WriteLine($"  Success: {execution.Success}");
            Console.WriteLine($"  Duration: {execution.Duration.TotalMilliseconds:F0}ms");
        }

        Console.WriteLine();
    }
}
