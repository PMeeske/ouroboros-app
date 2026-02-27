// <copyright file="IntelligentToolLearner.Analysis.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using Ouroboros.Tools.MeTTa;

/// <summary>
/// MeTTa analysis, LLM inference, and relevance checking for IntelligentToolLearner.
/// </summary>
public sealed partial class IntelligentToolLearner
{
    /// <summary>
    /// Analyzes a goal using MeTTa symbolic reasoning.
    /// </summary>
    private async Task<string> AnalyzeGoalWithMeTTaAsync(string goal, CancellationToken ct)
    {
        try
        {
            // Add the goal to MeTTa for reasoning
            string goalAtom = $"(goal current-goal \"{EscapeMeTTa(goal)}\")";
            await _mettaEngine.AddFactAsync(goalAtom, ct);

            // Query for capabilities needed
            string query = @"!(match &self
                (and
                    (goal current-goal $goal)
                    (or
                        (contains $goal ""search"")
                        (contains $goal ""find"")
                        (contains $goal ""fetch"")
                        (contains $goal ""calculate"")
                    )
                )
                $goal)";

            var result = await _mettaEngine.ExecuteQueryAsync(query, ct);
            return result.IsSuccess ? result.Value : goal;
        }
        catch
        {
            return goal;
        }
    }

    /// <summary>
    /// Infers tool specification from goal using LLM.
    /// </summary>
    private async Task<(string Name, string Description)> InferToolSpecFromGoalAsync(
        string goal,
        string mettaAnalysis,
        CancellationToken ct)
    {
        string prompt = $@"Based on this user goal, suggest a tool name and description.
Goal: {goal}
Analysis: {mettaAnalysis}

Respond in JSON format:
{{
  ""toolName"": ""snake_case_name"",
  ""description"": ""What the tool does""
}}";

        try
        {
            string response = await _llm.InnerModel.GenerateTextAsync(prompt, ct);

            // Parse JSON response
            var match = System.Text.RegularExpressions.Regex.Match(
                response,
                @"\{[^}]+""toolName""\s*:\s*""([^""]+)""[^}]+""description""\s*:\s*""([^""]+)""[^}]*\}",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (match.Success)
            {
                return (match.Groups[1].Value, match.Groups[2].Value);
            }

            // Fallback: generate from goal
            string safeName = goal.ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_");
            safeName = System.Text.RegularExpressions.Regex.Replace(safeName, @"[^a-z0-9_]", string.Empty);
            if (safeName.Length > 30) safeName = safeName[..30];

            return (safeName + "_tool", $"Tool to accomplish: {goal}");
        }
        catch
        {
            return ("dynamic_tool", goal);
        }
    }

    /// <summary>
    /// Computes word overlap ratio between two strings (Jaccard-like similarity).
    /// Returns a value between 0 and 1 representing the proportion of shared words.
    /// </summary>
    private static double ComputeOverlapRatio(string a, string b)
    {
        var wordsA = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var wordsB = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (wordsA.Count == 0 || wordsB.Count == 0)
            return 0.0;

        int intersection = wordsA.Intersect(wordsB).Count();
        int union = wordsA.Union(wordsB).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    /// <summary>
    /// Determines if a tool name is semantically relevant to the given goal.
    /// Prevents false matches where embeddings are similar but topics differ.
    /// </summary>
    /// <param name="toolName">The tool name (e.g., "net_10_migration_assistant").</param>
    /// <param name="goal">The user's goal (e.g., "learn to fly").</param>
    /// <returns>True if the tool seems relevant to the goal.</returns>
    private static bool IsToolRelevantToGoal(string toolName, string goal)
    {
        // Normalize: split tool name on underscores and goal on spaces
        var toolWords = toolName.ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        var goalWords = goal.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2) // Skip small words like "to", "a", etc.
            .ToHashSet();

        // Check for any meaningful word overlap
        var intersection = toolWords.Intersect(goalWords).ToList();
        if (intersection.Count > 0)
            return true;

        // Check for semantic keyword groups that should match
        var technicalKeywords = new HashSet<string> { "net", "dotnet", "migration", "upgrade", "version", "framework", "code", "assistant", "api", "sdk" };
        var learnKeywords = new HashSet<string> { "learn", "study", "understand", "know", "master", "skill", "tutorial", "course" };
        var flightKeywords = new HashSet<string> { "fly", "flight", "aviation", "pilot", "plane", "aircraft", "air" };
        var independenceKeywords = new HashSet<string> { "independent", "autonomy", "autonomous", "self", "freedom", "free" };

        // If tool is technical but goal is about learning/flying/independence, they don't match
        bool toolIsTechnical = toolWords.Intersect(technicalKeywords).Any();
        bool goalIsFlight = goalWords.Intersect(flightKeywords).Any();
        bool goalIsIndependence = goalWords.Intersect(independenceKeywords).Any();

        // Mismatch: technical tool for non-technical goal
        if (toolIsTechnical && (goalIsFlight || goalIsIndependence))
            return false;

        // If no clear mismatch found and the overlap ratio is at least minimal
        return ComputeOverlapRatio(toolName.Replace("_", " "), goal) >= 0.15;
    }

    /// <summary>
    /// Initializes MeTTa with tool knowledge.
    /// </summary>
    private async Task InitializeMeTTaKnowledgeAsync(CancellationToken ct)
    {
        try
        {
            // Add base tool capability rules
            string rules = @"
; Tool capability inference rules
(= (requires-search $goal)
   (or (contains $goal ""search"") (contains $goal ""find"") (contains $goal ""look up"")))

(= (requires-fetch $goal)
   (or (contains $goal ""fetch"") (contains $goal ""get"") (contains $goal ""download"")))

(= (requires-calculate $goal)
   (or (contains $goal ""calculate"") (contains $goal ""compute"") (contains $goal ""math"")))

; Tool selection rule
(= (best-tool-type $goal)
   (if (requires-search $goal) search-tool
       (if (requires-fetch $goal) fetch-tool
           (if (requires-calculate $goal) calculator-tool
               generic-tool))))
";
            await _mettaEngine.AddFactAsync(rules, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] MeTTa initialization failed: {ex.Message}");
        }
    }

    private static string EscapeMeTTa(string text) =>
        text.Replace("\"", "\\\"").Replace("\n", "\\n");
}
