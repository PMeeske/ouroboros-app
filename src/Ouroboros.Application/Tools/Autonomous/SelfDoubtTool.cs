// <copyright file="SelfDoubtTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Self-doubt tool that questions its own outputs.
/// Provides metacognitive check against overconfidence.
/// </summary>
public class SelfDoubtTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public SelfDoubtTool(IAutonomousToolContext context) => _ctx = context;
    public SelfDoubtTool() : this(AutonomousTools.DefaultContext) { }

    public string Name => "self_doubt";
    public string Description => "Question my own response for errors, biases, or overconfidence. Metacognitive check. Input: JSON {\"response\":\"...\", \"context\":\"...\"}";
    public string? JsonSchema => null;

    /// <summary>
    /// Delegate for LLM critique. Delegates to <see cref="IAutonomousToolContext.CritiqueFunction"/>.
    /// </summary>
    public static Func<string, CancellationToken, Task<string>>? CritiqueFunction
    {
        get => AutonomousTools.DefaultContext.CritiqueFunction;
        set => AutonomousTools.DefaultContext.CritiqueFunction = value;
    }

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var response = doc.RootElement.GetProperty("response").GetString() ?? "";
            var context = doc.RootElement.TryGetProperty("context", out var ctx) ? ctx.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(response))
                return Result<string, string>.Failure("Response to doubt is required.");

            if (_ctx.CritiqueFunction == null)
                return Result<string, string>.Failure("Critique function not available.");

            var prompt = $@"You are a critical reviewer. Examine this AI response for potential issues.

CONTEXT: {context}

AI RESPONSE TO EXAMINE:
{response}

Analyze for:
1. FACTUAL ERRORS: Any claims that might be wrong or unverifiable?
2. LOGICAL FLAWS: Any reasoning errors or non-sequiturs?
3. OVERCONFIDENCE: Where is certainty expressed that isn't warranted?
4. BIASES: Any hidden assumptions or perspectives that might skew the answer?
5. MISSING CONTEXT: What important considerations were left out?
6. HALLUCINATION RISK: Which parts seem most likely to be fabricated?

For each issue found, rate severity (LOW/MEDIUM/HIGH) and suggest correction.
If the response seems solid, acknowledge that too.";

            var critique = await _ctx.CritiqueFunction(prompt, ct);

            var sb = new StringBuilder();
            sb.AppendLine("\ud83e\udd14 **Self-Doubt Analysis**\n");
            sb.AppendLine(critique);

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Self-doubt failed: {ex.Message}");
        }
    }
}
