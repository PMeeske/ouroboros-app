using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Fitness function for action sequences using LLM evaluation.
/// </summary>
public sealed class ActionSequenceFitness : IFitnessFunction<ActionGene>
{
    private readonly ToolAwareChatModel _llm;
    private readonly string _goal;

    public ActionSequenceFitness(ToolAwareChatModel llm, string goal)
    {
        _llm = llm;
        _goal = goal;
    }

    public async Task<double> EvaluateAsync(IChromosome<ActionGene> chromosome)
    {
        var actions = chromosome.Genes
            .OrderByDescending(g => g.Priority)
            .Select(g => $"{g.ActionType}:{g.ActionName}")
            .ToList();

        string sequence = string.Join(" -> ", actions);

        string prompt = $@"Rate how well this action sequence would accomplish the goal (0-100):
Goal: {_goal}
Sequence: {sequence}

Consider: relevance, efficiency, completeness.
Just respond with a number 0-100.";

        try
        {
            string response = await _llm.InnerModel.GenerateTextAsync(prompt);
            var match = System.Text.RegularExpressions.Regex.Match(response, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int score))
            {
                // Normalize and add bonus for shorter sequences
                double normalizedScore = score / 100.0;
                double lengthBonus = Math.Max(0, (5 - actions.Count) * 0.05);
                return Math.Min(normalizedScore + lengthBonus, 1.0);
            }
        }
        catch
        {
            // Ignore errors
        }

        // Heuristic fallback
        return 0.3 + (chromosome.Genes.Count > 0 ? 0.2 : 0.0);
    }
}