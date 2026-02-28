// <copyright file="VerifyClaimTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Verification tool for fact-checking and reducing hallucinations.
/// Cross-references claims against multiple sources.
/// </summary>
public class VerifyClaimTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public VerifyClaimTool(IAutonomousToolContext context) => _ctx = context;
    public VerifyClaimTool() : this(AutonomousTools.DefaultContext) { }

    public string Name => "verify_claim";
    public string Description => "Verify a claim or fact by cross-referencing multiple sources. Reduces hallucination risk. Input: JSON {\"claim\":\"...\", \"depth\":\"quick|thorough\"}";
    public string? JsonSchema => null;

    /// <summary>
    /// Delegate for web search function. Delegates to <see cref="IAutonomousToolContext.SearchFunction"/>.
    /// </summary>
    public static Func<string, CancellationToken, Task<string>>? SearchFunction
    {
        get => AutonomousTools.DefaultContext.SearchFunction;
        set => AutonomousTools.DefaultContext.SearchFunction = value;
    }

    /// <summary>
    /// Delegate for LLM evaluation function. Delegates to <see cref="IAutonomousToolContext.EvaluateFunction"/>.
    /// </summary>
    public static Func<string, CancellationToken, Task<string>>? EvaluateFunction
    {
        get => AutonomousTools.DefaultContext.EvaluateFunction;
        set => AutonomousTools.DefaultContext.EvaluateFunction = value;
    }

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var claim = doc.RootElement.GetProperty("claim").GetString() ?? "";
            var depth = doc.RootElement.TryGetProperty("depth", out var d) ? d.GetString() ?? "quick" : "quick";

            if (string.IsNullOrWhiteSpace(claim))
                return Result<string, string>.Failure("Claim is required.");

            var sb = new StringBuilder();
            sb.AppendLine($"\ud83d\udd0d **Verification Report**");
            sb.AppendLine($"**Claim:** {claim}");
            sb.AppendLine();

            var evidence = new List<(string source, string content, double confidence)>();

            // Search for supporting/contradicting evidence
            if (_ctx.SearchFunction != null)
            {
                var searchQueries = new[] { claim, $"is it true that {claim}", $"{claim} fact check" };
                var searchTasks = depth == "thorough"
                    ? searchQueries.Select(q => _ctx.SearchFunction(q, ct))
                    : new[] { _ctx.SearchFunction(searchQueries[0], ct) };

                var results = await Task.WhenAll(searchTasks);

                for (int i = 0; i < results.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(results[i]))
                    {
                        evidence.Add(($"Search {i + 1}", results[i].Substring(0, Math.Min(500, results[i].Length)), 0.5));
                    }
                }
            }

            // Use LLM to evaluate evidence
            if (_ctx.EvaluateFunction != null && evidence.Count > 0)
            {
                var evalPrompt = new StringBuilder();
                evalPrompt.AppendLine("Evaluate this claim against the evidence. Be critical and skeptical.");
                evalPrompt.AppendLine($"CLAIM: {claim}");
                evalPrompt.AppendLine("\nEVIDENCE:");
                foreach (var (source, content, _) in evidence)
                {
                    evalPrompt.AppendLine($"[{source}]: {content}");
                }
                evalPrompt.AppendLine("\nRespond with:");
                evalPrompt.AppendLine("VERDICT: SUPPORTED/CONTRADICTED/UNCERTAIN/NEEDS_CONTEXT");
                evalPrompt.AppendLine("CONFIDENCE: 0-100%");
                evalPrompt.AppendLine("REASONING: Brief explanation");
                evalPrompt.AppendLine("CAVEATS: Any important qualifications");

                var evaluation = await _ctx.EvaluateFunction(evalPrompt.ToString(), ct);
                sb.AppendLine("**Analysis:**");
                sb.AppendLine(evaluation);
            }
            else if (evidence.Count == 0)
            {
                sb.AppendLine("\u26a0\ufe0f **No external evidence found.** Unable to verify.");
                sb.AppendLine("Consider this claim unverified. Treat with appropriate skepticism.");
            }
            else
            {
                sb.AppendLine("**Raw Evidence:**");
                foreach (var (source, content, _) in evidence.Take(3))
                {
                    sb.AppendLine($"\u2022 [{source}]: {content.Substring(0, Math.Min(100, content.Length))}...");
                }
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"Verification failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Verification failed: {ex.Message}");
        }
    }
}
