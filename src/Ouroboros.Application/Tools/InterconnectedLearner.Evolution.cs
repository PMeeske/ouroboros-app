// <copyright file="InterconnectedLearner.Evolution.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Ouroboros.Tools;

/// <summary>
/// Partial class containing genetic algorithm evolution, pattern learning, smart suggestions, and helpers.
/// </summary>
public sealed partial class InterconnectedLearner
{
    #region Genetic Algorithm Evolution

    /// <summary>
    /// Uses genetic algorithm to evolve optimal action sequences for a goal.
    /// </summary>
    public async Task<List<(string ActionType, string ActionName)>> EvolveOptimalSequenceAsync(
        string goal,
        ToolRegistry availableTools,
        int maxSteps = 5,
        CancellationToken ct = default)
    {
        if (_llm == null)
            return new List<(string, string)>();

        try
        {
            // Get available actions
            var toolNames = availableTools.All.Select(t => ("tool", t.Name)).ToList();
            var skillNames = (_skillRegistry?.GetAllSkills().Select(s => ("skill", s.Name)).ToList())
                ?? new List<(string, string)>();
            var allActions = toolNames.Concat(skillNames).ToList();

            if (allActions.Count == 0)
                return new List<(string, string)>();

            // Create initial population
            var initialPopulation = CreateActionSequencePopulation(allActions, maxSteps, 15);

            // Create fitness function
            var fitnessFunc = new ActionSequenceFitness(_llm, goal);

            // Create genetic algorithm
            var ga = new GeneticAlgorithm<ActionGene>(
                fitnessFunc,
                MutateActionGene,
                mutationRate: 0.2,
                crossoverRate: 0.7,
                elitismRate: 0.15);

            // Evolve for a few generations
            var result = await ga.EvolveAsync(initialPopulation, generations: 8, ct);

            if (result.IsSuccess)
            {
                var bestChromosome = result.Value;
                return bestChromosome.Genes
                    .OrderByDescending(g => g.Priority)
                    .Take(maxSteps)
                    .Select(g => (g.ActionType, g.ActionName))
                    .ToList();
            }
        }
        catch
        {
            // Fallback
        }

        return new List<(string, string)>();
    }

    private List<IChromosome<ActionGene>> CreateActionSequencePopulation(
        List<(string Type, string Name)> availableActions,
        int maxSteps,
        int populationSize)
    {
        var random = new Random();
        var population = new List<IChromosome<ActionGene>>();

        for (int i = 0; i < populationSize; i++)
        {
            var genes = new List<ActionGene>();
            int numActions = random.Next(1, maxSteps + 1);

            for (int j = 0; j < numActions; j++)
            {
                var action = availableActions[random.Next(availableActions.Count)];
                genes.Add(new ActionGene(action.Type, action.Name, 1.0 - (j * 0.1)));
            }

            population.Add(new ActionSequenceChromosome(genes));
        }

        return population;
    }

    private static ActionGene MutateActionGene(ActionGene gene)
    {
        var random = new Random();

        // Mutate priority
        double newPriority = Math.Clamp(gene.Priority + (random.NextDouble() - 0.5) * 0.3, 0.0, 1.0);

        return gene with { Priority = newPriority };
    }

    #endregion

    #region Pattern Learning

    private void UpdateConceptGraph(ExecutionRecord record)
    {
        // Extract concepts from input
        var concepts = ExtractKeywords(record.Input);

        // Link action to concepts
        if (!_conceptGraph.ContainsKey(record.Name))
        {
            _conceptGraph[record.Name] = new List<string>();
        }

        foreach (var concept in concepts)
        {
            if (!_conceptGraph[record.Name].Contains(concept))
            {
                _conceptGraph[record.Name].Add(concept);
            }

            // Reverse link: concept -> action
            if (!_conceptGraph.ContainsKey(concept))
            {
                _conceptGraph[concept] = new List<string>();
            }

            if (!_conceptGraph[concept].Contains(record.Name))
            {
                _conceptGraph[concept].Add(record.Name);
            }
        }
    }

    private async Task TryLearnPatternsAsync(CancellationToken ct)
    {
        var recentExecutions = _executionLog.ToArray().TakeLast(20).ToList();
        if (recentExecutions.Count < 5) return;

        // Find sequences of successful executions
        var successfulSequence = new List<ExecutionRecord>();
        foreach (var exec in recentExecutions)
        {
            if (exec.Success)
            {
                successfulSequence.Add(exec);
            }
            else
            {
                if (successfulSequence.Count >= 2)
                {
                    await CreatePatternFromSequenceAsync(successfulSequence, ct);
                }

                successfulSequence.Clear();
            }
        }

        // Handle trailing successful sequence
        if (successfulSequence.Count >= 2)
        {
            await CreatePatternFromSequenceAsync(successfulSequence, ct);
        }
    }

    private async Task CreatePatternFromSequenceAsync(List<ExecutionRecord> sequence, CancellationToken ct)
    {
        if (sequence.Count < 2) return;

        // Generate description from inputs
        string goalDescription = string.Join(" -> ", sequence.Select(s => Truncate(s.Input, 50)));

        // Get embedding if available
        float[] embedding = Array.Empty<float>();
        if (_embeddingModel != null)
        {
            try
            {
                embedding = await _embeddingModel.CreateEmbeddingsAsync(goalDescription, ct);
            }
            catch
            {
                // Continue without embedding
            }
        }

        var pattern = new InterconnectedPattern(
            Id: Guid.NewGuid().ToString("N"),
            PatternType: DeterminePatternType(sequence),
            GoalDescription: goalDescription,
            ToolSequence: sequence.Where(s => s.ExecutionType == "tool").Select(s => s.Name).ToList(),
            SkillSequence: sequence.Where(s => s.ExecutionType == "skill").Select(s => s.Name).ToList(),
            SuccessRate: 1.0,
            UsageCount: 1,
            CreatedAt: DateTime.UtcNow,
            LastUsed: DateTime.UtcNow,
            EmbeddingVector: embedding);

        _patterns[pattern.Id] = pattern;
    }

    private static string DeterminePatternType(List<ExecutionRecord> sequence)
    {
        bool hasTools = sequence.Any(s => s.ExecutionType == "tool");
        bool hasSkills = sequence.Any(s => s.ExecutionType == "skill");
        bool hasPipelines = sequence.Any(s => s.ExecutionType == "pipeline");

        if (hasTools && hasSkills) return "hybrid";
        if (hasTools) return "tool-chain";
        if (hasSkills) return "skill-chain";
        if (hasPipelines) return "pipeline-chain";
        return "mixed";
    }

    #endregion

    #region Smart Suggestions

    /// <summary>
    /// Gets intelligent suggestions for accomplishing a goal using all available algorithms.
    /// </summary>
    public async Task<SmartSuggestion> SuggestForGoalAsync(
        string goal,
        ToolRegistry availableTools,
        CancellationToken ct = default)
    {
        var suggestion = new SmartSuggestion
        {
            Goal = goal,
            Timestamp = DateTime.UtcNow
        };

        // 1. MeTTa symbolic reasoning
        var mettaSuggestions = await QueryRelatedActionsAsync(goal, ct);
        suggestion.MeTTaSuggestions = mettaSuggestions;

        // 2. Embedding similarity search
        var similarPatterns = await FindSimilarPatternsAsync(goal, 3, ct);
        suggestion.SimilarPatterns = similarPatterns.Select(p => new PatternSummary(
            p.PatternType,
            p.GoalDescription,
            p.ToolSequence.Concat(p.SkillSequence).ToList(),
            p.SuccessRate)).ToList();

        // 3. LLM analysis
        var llmSuggestion = await SuggestActionWithLLMAsync(goal, availableTools, ct);
        if (llmSuggestion.HasValue)
        {
            suggestion.LLMSuggestion = new LLMActionSuggestion(
                llmSuggestion.Value.ActionType,
                llmSuggestion.Value.ActionName,
                llmSuggestion.Value.Reasoning);
        }

        // 4. Genetic algorithm optimization (for complex goals)
        if (goal.Length > 20 || goal.Contains(" and ") || goal.Contains(","))
        {
            var evolvedSequence = await EvolveOptimalSequenceAsync(goal, availableTools, 5, ct);
            suggestion.EvolvedSequence = evolvedSequence;
        }

        // 5. Concept graph traversal
        suggestion.RelatedConcepts = GetRelatedConcepts(goal);

        // 6. Compute confidence score
        suggestion.ConfidenceScore = ComputeConfidenceScore(suggestion);

        return suggestion;
    }

    private List<string> GetRelatedConcepts(string goal)
    {
        var keywords = ExtractKeywords(goal);
        var related = new HashSet<string>();

        foreach (var keyword in keywords)
        {
            if (_conceptGraph.TryGetValue(keyword, out var connections))
            {
                foreach (var connection in connections.Take(5))
                {
                    related.Add(connection);
                }
            }
        }

        return related.Take(10).ToList();
    }

    private static double ComputeConfidenceScore(SmartSuggestion suggestion)
    {
        double score = 0.0;

        // Weight different sources
        if (suggestion.MeTTaSuggestions.Count > 0) score += 0.2;
        if (suggestion.SimilarPatterns.Count > 0) score += 0.25 * Math.Min(suggestion.SimilarPatterns.Count, 3) / 3.0;
        if (suggestion.LLMSuggestion != null) score += 0.3;
        if (suggestion.EvolvedSequence.Count > 0) score += 0.15;
        if (suggestion.RelatedConcepts.Count > 0) score += 0.1;

        return Math.Min(score, 1.0);
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets learning statistics.
    /// </summary>
    public LearningStats GetStats() => new(
        TotalToolExecutions: _totalToolExecutions,
        TotalSkillExecutions: _totalSkillExecutions,
        TotalPipelineExecutions: _totalPipelineExecutions,
        SuccessfulExecutions: _successfulExecutions,
        LearnedPatterns: _patterns.Count,
        ConceptGraphNodes: _conceptGraph.Count,
        ExecutionLogSize: _executionLog.Count);

    /// <summary>
    /// Gets all learned patterns.
    /// </summary>
    public IReadOnlyList<InterconnectedPattern> GetPatterns() =>
        _patterns.Values.OrderByDescending(p => p.SuccessRate * p.UsageCount).ToList();

    #endregion

    #region Helpers

    private static string EscapeMeTTa(string text) =>
        text.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", string.Empty);

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "...";

    private static string ExtractCapability(string input)
    {
        // Extract first verb or action phrase
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 ? words[0].ToLowerInvariant() : "unknown";
    }

    private static List<string> ExtractKeywords(string text)
    {
        // Simple keyword extraction - remove common words
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could",
            "should", "may", "might", "must", "shall", "can", "to", "of", "in",
            "for", "on", "with", "at", "by", "from", "as", "into", "through",
            "during", "before", "after", "above", "below", "between", "under",
            "again", "further", "then", "once", "here", "there", "when", "where",
            "why", "how", "all", "each", "few", "more", "most", "other", "some",
            "such", "no", "nor", "not", "only", "own", "same", "so", "than",
            "too", "very", "just", "and", "but", "if", "or", "because", "until",
            "while", "about", "against", "this", "that", "these", "those", "it"
        };

        return text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct()
            .Take(15)
            .ToList();
    }

    #endregion
}
