
namespace Ouroboros.Application;

/// <summary>
/// Partial class for self-improvement steps: evaluation, iterative improvement, and learning.
/// </summary>
public static partial class SelfImprovementCliSteps
{
    // ═══════════════════════════════════════════════════════════════════════════
    // SELF-IMPROVEMENT STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Evaluates the current pipeline output and suggests improvements.
    /// Uses LLM to analyze output quality and provide feedback.
    /// Usage: SelfEvaluate or SelfEvaluate('criteria')
    /// </summary>
    [PipelineToken("SelfEvaluate", "Evaluate")]
    public static Step<CliPipelineState, CliPipelineState> SelfEvaluate(string? args = null)
    {
        var criteria = CliSteps.ParseString(args ?? "quality, completeness, accuracy");
        return TrackedStep.Wrap(
            async s =>
            {
                var currentOutput = s.Output ?? s.Context;

                if (string.IsNullOrWhiteSpace(currentOutput))
                {
                    if (s.Trace)
                    {
                        Console.WriteLine("[trace] No output to evaluate");
                    }
                    return s;
                }

                var prompt = $@"Evaluate the following output based on these criteria: {criteria}

OUTPUT TO EVALUATE:
{currentOutput}

Provide a structured evaluation with:
1. Overall score (1-10)
2. Strengths
3. Weaknesses
4. Specific improvement suggestions

Be concise but thorough.";

                var evaluation = await s.Llm.InnerModel.GenerateTextAsync(prompt);

                s.Branch = s.Branch.WithIngestEvent(
                    "self-evaluate:complete",
                    new[] { criteria, evaluation[..Math.Min(200, evaluation.Length)] });

                if (s.Trace)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n=== Self-Evaluation ===");
                    Console.WriteLine(evaluation);
                    Console.WriteLine("=======================\n");
                    Console.ResetColor();
                }

                // Append evaluation to output
                s.Output = $"{currentOutput}\n\n--- Self-Evaluation ---\n{evaluation}";

                return s;
            },
            "SelfEvaluate",
            new[] { "Evaluate" },
            nameof(SelfImprovementCliSteps),
            "Evaluates the current output and suggests improvements",
            args);
    }

    /// <summary>
    /// Attempts to improve the current output based on self-evaluation.
    /// Usage: SelfImprove or SelfImprove('2') for multiple iterations
    /// </summary>
    [PipelineToken("SelfImprove", "Improve")]
    public static Step<CliPipelineState, CliPipelineState> SelfImprove(string? args = null)
    {
        var iterations = 1;
        if (int.TryParse(CliSteps.ParseString(args ?? "1"), out var parsed))
        {
            iterations = Math.Max(1, Math.Min(5, parsed)); // Clamp 1-5
        }

        return TrackedStep.Wrap(
            async s =>
            {
                var currentOutput = s.Output ?? s.Context;
                if (string.IsNullOrWhiteSpace(currentOutput))
                {
                    return s;
                }

                for (int i = 0; i < iterations; i++)
                {
                    var prompt = $@"Improve the following output. Make it clearer, more accurate, and more complete.
Keep the same general structure but enhance the quality.

CURRENT OUTPUT:
{currentOutput}

IMPROVED VERSION:";

                    var improved = await s.Llm.InnerModel.GenerateTextAsync(prompt);

                    s.Branch = s.Branch.WithIngestEvent(
                        $"self-improve:iteration:{i + 1}",
                        new[] { currentOutput.Length.ToString(), improved.Length.ToString() });

                    currentOutput = improved;

                    if (s.Trace)
                    {
                        Console.WriteLine($"[trace] Self-improvement iteration {i + 1} complete");
                    }
                }

                s.Output = currentOutput;
                return s;
            },
            "SelfImprove",
            new[] { "Improve" },
            nameof(SelfImprovementCliSteps),
            "Iteratively improves the current output",
            args);
    }

    /// <summary>
    /// Learns from the current pipeline execution by extracting patterns.
    /// Usage: Learn('topic-name')
    /// </summary>
    [PipelineToken("Learn", "ExtractLearning")]
    public static Step<CliPipelineState, CliPipelineState> Learn(string? args = null)
    {
        var topic = CliSteps.ParseString(args ?? "general");
        return TrackedStep.Wrap(
            async s =>
            {
                var context = s.Context;
                var output = s.Output;

                var prompt = $@"Extract key learnings from this pipeline execution.

TOPIC: {topic}
CONTEXT: {context}
OUTPUT: {output}

Provide:
1. Key insights (bullet points)
2. Patterns observed
3. Potential improvements for next time
4. Knowledge to remember

Format as structured text for storage.";

                var learnings = await s.Llm.InnerModel.GenerateTextAsync(prompt);

                s.Branch = s.Branch.WithIngestEvent(
                    $"learn:{topic}",
                    new[] { learnings[..Math.Min(500, learnings.Length)] });

                if (s.Trace)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n=== Learnings Extracted ===");
                    Console.WriteLine(learnings);
                    Console.WriteLine("===========================\n");
                    Console.ResetColor();
                }

                // Store learnings in output for potential persistence
                s.Output = $"{s.Output}\n\n--- Learnings ---\n{learnings}";

                return s;
            },
            "Learn",
            new[] { "ExtractLearning" },
            nameof(SelfImprovementCliSteps),
            "Extracts learnings from the pipeline execution",
            args);
    }
}
