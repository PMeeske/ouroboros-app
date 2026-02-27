// <copyright file="ReasoningChainTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Structured reasoning chain tool that enforces step-by-step logic.
/// Prevents pattern-matching shortcuts by requiring explicit derivation steps.
/// </summary>
public class ReasoningChainTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public ReasoningChainTool(IAutonomousToolContext context) => _ctx = context;
    public ReasoningChainTool() : this(AutonomousTools.DefaultContext) { }

    public string Name => "reasoning_chain";
    public string Description => "Execute structured step-by-step reasoning. Enforces logical derivation instead of pattern matching. Input: JSON {\"problem\":\"...\", \"mode\":\"deductive|inductive|abductive\"}";
    public string? JsonSchema => null;

    /// <summary>
    /// Delegate for LLM reasoning function. Delegates to <see cref="IAutonomousToolContext.ReasonFunction"/>.
    /// </summary>
    public static Func<string, CancellationToken, Task<string>>? ReasonFunction
    {
        get => AutonomousTools.DefaultContext.ReasonFunction;
        set => AutonomousTools.DefaultContext.ReasonFunction = value;
    }

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var problem = doc.RootElement.GetProperty("problem").GetString() ?? "";
            var mode = doc.RootElement.TryGetProperty("mode", out var m) ? m.GetString() ?? "deductive" : "deductive";

            if (string.IsNullOrWhiteSpace(problem))
                return Result<string, string>.Failure("Problem is required.");

            var sb = new StringBuilder();
            sb.AppendLine($"\ud83d\udd17 **Reasoning Chain** ({mode})");
            sb.AppendLine($"**Problem:** {problem}");
            sb.AppendLine();

            if (_ctx.ReasonFunction == null)
                return Result<string, string>.Failure("Reasoning function not available.");

            // Multi-step structured reasoning
            var steps = new List<(string step, string result)>();

            // Step 1: Decompose
            var decomposePrompt = $"DECOMPOSITION STEP: Break down this problem into 2-4 sub-questions that must be answered to solve it.\nPROBLEM: {problem}\n\nList each sub-question on its own line, numbered 1-4.";
            var decomposed = await _ctx.ReasonFunction(decomposePrompt, ct);
            steps.Add(("Decomposition", decomposed));

            // Step 2: For each sub-question, derive
            var derivePrompt = mode switch
            {
                "deductive" => $"DEDUCTIVE REASONING: Starting from known facts and rules, derive conclusions for:\n{decomposed}\n\nFor each sub-question:\n1. State the relevant facts/axioms\n2. Apply logical rules\n3. State the derived conclusion\n\nShow your work explicitly.",
                "inductive" => $"INDUCTIVE REASONING: From the patterns and examples available, generalize:\n{decomposed}\n\nFor each sub-question:\n1. List relevant examples/observations\n2. Identify the pattern\n3. State the generalized principle\n\nShow your work explicitly.",
                "abductive" => $"ABDUCTIVE REASONING: Find the best explanation for:\n{decomposed}\n\nFor each sub-question:\n1. List possible explanations\n2. Evaluate plausibility of each\n3. Select the most likely explanation\n\nShow your work explicitly.",
                _ => $"REASONING: Answer these sub-questions systematically:\n{decomposed}\n\nShow your work explicitly."
            };
            var derived = await _ctx.ReasonFunction(derivePrompt, ct);
            steps.Add(("Derivation", derived));

            // Step 3: Synthesize
            var synthesizePrompt = $"SYNTHESIS STEP: Combine the derived answers into a final solution.\n\nORIGINAL PROBLEM: {problem}\n\nDERIVED CONCLUSIONS:\n{derived}\n\nProvide:\n1. ANSWER: The direct answer to the problem\n2. CONFIDENCE: How certain (0-100%)\n3. LIMITATIONS: What assumptions were made or what could be wrong";
            var synthesis = await _ctx.ReasonFunction(synthesizePrompt, ct);
            steps.Add(("Synthesis", synthesis));

            // Format output
            foreach (var (step, result) in steps)
            {
                sb.AppendLine($"### {step}");
                sb.AppendLine(result);
                sb.AppendLine();
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Reasoning chain failed: {ex.Message}");
        }
    }
}
