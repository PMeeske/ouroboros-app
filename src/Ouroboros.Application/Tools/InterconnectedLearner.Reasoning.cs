// <copyright file="InterconnectedLearner.Reasoning.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Text;
using Ouroboros.Tools;

/// <summary>
/// Partial class containing MeTTa symbolic reasoning, LLM-based analysis, and embedding similarity.
/// </summary>
public sealed partial class InterconnectedLearner
{
    #region MeTTa Symbolic Reasoning

    private async Task AddToMeTTaKnowledgeAsync(ExecutionRecord record, CancellationToken ct)
    {
        try
        {
            // Add execution fact
            string executionAtom = $"(execution {record.ExecutionType} \"{EscapeMeTTa(record.Name)}\" {(record.Success ? "success" : "failure")})";
            await _mettaEngine.AddFactAsync(executionAtom, ct);

            // Add tool/skill capability fact
            string capabilityAtom = $"(capability \"{EscapeMeTTa(record.Name)}\" \"{EscapeMeTTa(ExtractCapability(record.Input))}\")";
            await _mettaEngine.AddFactAsync(capabilityAtom, ct);

            // Add relationship facts based on input patterns
            var keywords = ExtractKeywords(record.Input);
            foreach (var keyword in keywords.Take(5)) // Limit to 5 most relevant
            {
                string relationAtom = $"(related-to \"{EscapeMeTTa(record.Name)}\" \"{EscapeMeTTa(keyword)}\")";
                await _mettaEngine.AddFactAsync(relationAtom, ct);
            }
        }
        catch
        {
            // MeTTa updates are best-effort
        }
    }

    /// <summary>
    /// Queries MeTTa to find tools/skills related to a goal.
    /// </summary>
    public async Task<List<string>> QueryRelatedActionsAsync(string goal, CancellationToken ct = default)
    {
        var results = new List<string>();

        try
        {
            // Query for tools/skills that can handle this goal
            string query = $@"!(match &self
                (and
                    (capability $name $cap)
                    (or
                        (contains ""{EscapeMeTTa(goal)}"" $cap)
                        (related-to $name ""{EscapeMeTTa(goal)}"")
                    )
                )
                $name)";

            var mettaResult = await _mettaEngine.ExecuteQueryAsync(query, ct);
            if (mettaResult.IsSuccess && !string.IsNullOrWhiteSpace(mettaResult.Value))
            {
                // Parse MeTTa results
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    mettaResult.Value,
                    @"""([^""]+)""");

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        results.Add(match.Groups[1].Value);
                    }
                }
            }
        }
        catch
        {
            // Fallback to concept graph
        }

        return results.Distinct().ToList();
    }

    #endregion

    #region LLM-Based Pattern Analysis

    /// <summary>
    /// Uses LLM to analyze execution patterns and suggest improvements.
    /// </summary>
    public async Task<string> AnalyzePatternsWithLLMAsync(CancellationToken ct = default)
    {
        if (_llm == null)
            return "LLM not available for pattern analysis.";

        var recentExecutions = _executionLog.ToArray().TakeLast(50).ToList();
        if (recentExecutions.Count < 5)
            return "Not enough execution data for pattern analysis.";

        var sb = new StringBuilder();
        sb.AppendLine("Recent execution history:");
        foreach (var exec in recentExecutions.TakeLast(20))
        {
            sb.AppendLine($"- [{exec.ExecutionType}] {exec.Name}: {(exec.Success ? "✓" : "✗")} ({exec.Duration.TotalMilliseconds:F0}ms)");
            sb.AppendLine($"  Input: {Truncate(exec.Input, 100)}");
        }

        string prompt = $@"Analyze this AI system's execution history and provide insights:

{sb}

Provide:
1. Patterns you notice (what tools/skills are used together)
2. Optimization suggestions (faster alternatives, better sequences)
3. Skills that could be created to automate common patterns
4. Any inefficiencies or issues

Be concise and actionable.";

        try
        {
            var response = await _llm.InnerModel.GenerateTextAsync(prompt, ct);
            return response;
        }
        catch (Exception ex)
        {
            return $"Pattern analysis failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Uses LLM to suggest the best action for a given goal.
    /// </summary>
    public async Task<(string ActionType, string ActionName, string Reasoning)?> SuggestActionWithLLMAsync(
        string goal,
        ToolRegistry availableTools,
        CancellationToken ct = default)
    {
        if (_llm == null)
            return null;

        // Gather context
        var toolNames = availableTools.All.Select(t => t.Name).ToList();
        var skillNames = _skillRegistry?.GetAllSkills().Select(s => s.Name).ToList() ?? new List<string>();
        var relatedFromMeTTa = await QueryRelatedActionsAsync(goal, ct);
        var similarPatterns = await FindSimilarPatternsAsync(goal, 3, ct);

        string prompt = $@"Goal: {goal}

Available Tools: {string.Join(", ", toolNames)}
Available Skills: {string.Join(", ", skillNames)}
MeTTa suggested: {string.Join(", ", relatedFromMeTTa)}
Similar past patterns: {string.Join("; ", similarPatterns.Select(p => $"{p.PatternType}: {string.Join("->", p.ToolSequence.Concat(p.SkillSequence))}"))}

What single action should be taken? Respond in JSON:
{{
  ""actionType"": ""tool"" or ""skill"" or ""pipeline"" or ""create_tool"" or ""create_skill"",
  ""actionName"": ""name of action to take"",
  ""reasoning"": ""why this is the best choice""
}}";

        try
        {
            var response = await _llm.InnerModel.GenerateTextAsync(prompt, ct);

            // Parse JSON
            var match = System.Text.RegularExpressions.Regex.Match(
                response,
                @"""actionType""\s*:\s*""([^""]+)"".*?""actionName""\s*:\s*""([^""]+)"".*?""reasoning""\s*:\s*""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (match.Success)
            {
                return (match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
            }
        }
        catch
        {
            // Fallback
        }

        return null;
    }

    #endregion

    #region Embedding-Based Similarity

    /// <summary>
    /// Finds patterns semantically similar to the given goal using embeddings.
    /// </summary>
    public async Task<List<InterconnectedPattern>> FindSimilarPatternsAsync(
        string goal,
        int topK = 5,
        CancellationToken ct = default)
    {
        if (_embeddingModel == null || _patterns.IsEmpty)
            return new List<InterconnectedPattern>();

        try
        {
            // Get embedding for the goal
            var goalEmbedding = await _embeddingModel.CreateEmbeddingsAsync(goal, ct);

            // Compute cosine similarity with all patterns
            var similarities = new List<(InterconnectedPattern Pattern, double Similarity)>();

            foreach (var pattern in _patterns.Values)
            {
                if (pattern.EmbeddingVector.Length > 0)
                {
                    double similarity = CosineSimilarity(goalEmbedding, pattern.EmbeddingVector);
                    similarities.Add((pattern, similarity));
                }
            }

            return similarities
                .OrderByDescending(x => x.Similarity)
                .Take(topK)
                .Where(x => x.Similarity > 0.5) // Threshold
                .Select(x => x.Pattern)
                .ToList();
        }
        catch
        {
            return new List<InterconnectedPattern>();
        }
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        double denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator > 0 ? dotProduct / denominator : 0;
    }

    #endregion
}
