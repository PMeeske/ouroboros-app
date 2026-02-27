#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Text;

namespace Ouroboros.Application;

/// <summary>
/// Partial class for autonomous execution steps: planning, execution, reflection, and combined cycles.
/// </summary>
public static partial class SelfImprovementCliSteps
{
    // ═══════════════════════════════════════════════════════════════════════════
    // AUTONOMOUS EXECUTION STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Plans a complex task by decomposing it into sub-steps.
    /// Usage: Plan('task description')
    /// </summary>
    [PipelineToken("Plan", "Decompose")]
    public static Step<CliPipelineState, CliPipelineState> Plan(string? args = null)
    {
        var task = CliSteps.ParseString(args ?? "");
        return TrackedStep.Wrap(
            async s =>
            {
                var taskToUse = string.IsNullOrWhiteSpace(task) ? s.Query : task;

                if (string.IsNullOrWhiteSpace(taskToUse))
                {
                    return s;
                }

                var prompt = $@"Decompose this task into a step-by-step plan:

TASK: {taskToUse}

Provide a numbered list of concrete steps that could be executed as a pipeline.
Use DSL-style notation where possible (e.g., Set('topic') | UseDraft | UseCritique).
Each step should be clear and actionable.";

                var plan = await s.Llm.InnerModel.GenerateTextAsync(prompt);

                s.Branch = s.Branch.WithIngestEvent(
                    "plan:created",
                    new[] { taskToUse, plan[..Math.Min(300, plan.Length)] });

                s.Output = plan;
                s.Context = plan;

                if (s.Trace)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\n=== Execution Plan ===");
                    Console.WriteLine(plan);
                    Console.WriteLine("=====================\n");
                    Console.ResetColor();
                }

                return s;
            },
            "Plan",
            new[] { "Decompose" },
            nameof(SelfImprovementCliSteps),
            "Plans a complex task by decomposing into steps",
            args);
    }

    /// <summary>
    /// Executes a dynamically generated plan step by step.
    /// Usage: ExecutePlan
    /// </summary>
    [PipelineToken("ExecutePlan", "RunPlan")]
    public static Step<CliPipelineState, CliPipelineState> ExecutePlan(string? args = null)
        => TrackedStep.Wrap(
            async s =>
            {
                var plan = s.Context;

                if (string.IsNullOrWhiteSpace(plan))
                {
                    Console.WriteLine("[error] No plan available. Use 'Plan' first.");
                    return s;
                }

                // Extract DSL commands from plan
                var dslPattern = new System.Text.RegularExpressions.Regex(
                    @"([A-Z][a-zA-Z]+(?:\([^)]*\))?(?:\s*\|\s*[A-Z][a-zA-Z]+(?:\([^)]*\))?)*)",
                    System.Text.RegularExpressions.RegexOptions.Multiline);

                var matches = dslPattern.Matches(plan);
                var results = new StringBuilder();

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var dsl = match.Value.Trim();
                    if (dsl.Length > 3 && dsl.Contains('|'))
                    {
                        try
                        {
                            if (s.Trace)
                            {
                                Console.WriteLine($"[trace] Executing DSL: {dsl}");
                            }

                            var step = PipelineDsl.Build(dsl);
                            s = await step(s);
                            results.AppendLine($"✓ {dsl}");
                        }
                        catch (Exception ex)
                        {
                            results.AppendLine($"✗ {dsl}: {ex.Message}");
                        }
                    }
                }

                s.Branch = s.Branch.WithIngestEvent(
                    "plan:executed",
                    new[] { matches.Count.ToString() });

                s.Output = results.ToString();

                return s;
            },
            "ExecutePlan",
            new[] { "RunPlan" },
            nameof(SelfImprovementCliSteps),
            "Executes a dynamically generated plan",
            args);

    /// <summary>
    /// Reflects on the pipeline execution and generates insights.
    /// Usage: Reflect
    /// </summary>
    [PipelineToken("Reflect", "Introspect")]
    public static Step<CliPipelineState, CliPipelineState> Reflect(string? args = null)
        => TrackedStep.Wrap(
            async s =>
            {
                // Gather execution history
                var events = s.Branch.Events.TakeLast(20).ToList();
                var eventSummary = string.Join("\n", events.Select(e => $"- {e.GetType().Name}"));

                var prompt = $@"Reflect on this pipeline execution:

EXECUTION EVENTS:
{eventSummary}

TOPIC: {s.Topic}
QUERY: {s.Query}
OUTPUT LENGTH: {s.Output?.Length ?? 0} chars

Provide introspective analysis:
1. What went well?
2. What could be improved?
3. What patterns emerged?
4. Confidence in the output (1-10)?";

                var reflection = await s.Llm.InnerModel.GenerateTextAsync(prompt);

                s.Branch = s.Branch.WithIngestEvent(
                    "reflect:complete",
                    new[] { reflection[..Math.Min(200, reflection.Length)] });

                if (s.Trace)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("\n=== Reflection ===");
                    Console.WriteLine(reflection);
                    Console.WriteLine("==================\n");
                    Console.ResetColor();
                }

                s.Output = $"{s.Output}\n\n--- Reflection ---\n{reflection}";

                return s;
            },
            "Reflect",
            new[] { "Introspect" },
            nameof(SelfImprovementCliSteps),
            "Reflects on the pipeline execution",
            args);

    // ═══════════════════════════════════════════════════════════════════════════
    // COMBINED SELF-IMPROVING PIPELINE STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Complete self-improving reasoning cycle: Draft → Critique → Improve → Evaluate → Learn.
    /// Usage: SelfImprovingCycle('topic')
    /// </summary>
    [PipelineToken("SelfImprovingCycle", "SIC")]
    public static Step<CliPipelineState, CliPipelineState> SelfImprovingCycle(string? args = null)
    {
        var topic = CliSteps.ParseString(args ?? "");
        return TrackedStep.Wrap(
            async s =>
            {
                s.Topic = string.IsNullOrWhiteSpace(topic) ? s.Topic : topic;

                if (s.Trace)
                {
                    Console.WriteLine($"[trace] Starting self-improving cycle for: {s.Topic}");
                }

                // Enable reification
                s = s.WithNetworkTracking();

                // Build the self-improving pipeline
                var pipeline = new StepDefinition<CliPipelineState, CliPipelineState>(state => state)
                    | TrackCapability("reasoning")
                    | ReasoningCliSteps.UseDraft()
                    | Checkpoint("draft-complete")
                    | ReasoningCliSteps.UseCritique()
                    | Checkpoint("critique-complete")
                    | ReasoningCliSteps.UseImprove()
                    | Checkpoint("improve-complete")
                    | SelfEvaluate("clarity, accuracy, completeness")
                    | Learn(s.Topic)
                    | EndCapability("success")
                    | Reflect();

                s = await pipeline.Build()(s);

                return s;
            },
            "SelfImprovingCycle",
            new[] { "SIC" },
            nameof(SelfImprovementCliSteps),
            "Complete self-improving reasoning cycle",
            args);
    }

    /// <summary>
    /// Autonomous problem-solving with planning and execution.
    /// Usage: AutoSolve('problem description')
    /// </summary>
    [PipelineToken("AutoSolve", "Autonomous")]
    public static Step<CliPipelineState, CliPipelineState> AutoSolve(string? args = null)
    {
        var problem = CliSteps.ParseString(args ?? "");
        return TrackedStep.Wrap(
            async s =>
            {
                s.Query = string.IsNullOrWhiteSpace(problem) ? s.Query : problem;

                if (s.Trace)
                {
                    Console.WriteLine($"[trace] Starting autonomous problem-solving: {s.Query}");
                }

                // Enable reification
                s = s.WithNetworkTracking();

                // Phase 1: Planning
                s = await Plan(s.Query)(s);
                var plan = s.Output;

                // Phase 2: Self-improving execution
                s = await SelfImprovingCycle(s.Query)(s);

                // Phase 3: Final reflection and learning
                s.Context = $"Plan:\n{plan}\n\nExecution Result:\n{s.Output}";
                s = await Reflect()(s);

                return s;
            },
            "AutoSolve",
            new[] { "Autonomous" },
            nameof(SelfImprovementCliSteps),
            "Autonomous problem-solving with planning and execution",
            args);
    }
}
