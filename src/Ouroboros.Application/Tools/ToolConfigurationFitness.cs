using System.Text.RegularExpressions;
using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Fitness function for evaluating tool configurations.
/// </summary>
public sealed partial class ToolConfigurationFitness : IFitnessFunction<ToolConfigurationGene>
{
    private readonly ToolAwareChatModel _llm;
    private readonly string _goal;

    public ToolConfigurationFitness(ToolAwareChatModel llm, string goal)
    {
        _llm = llm;
        _goal = goal;
    }

    public async Task<double> EvaluateAsync(IChromosome<ToolConfigurationGene> chromosome,
        CancellationToken cancellationToken)
    {
        var config = ((ToolConfigurationChromosome)chromosome).ToConfiguration();

        // Use LLM to evaluate how well the configuration matches the goal
        string prompt = $@"Rate how well this tool configuration matches the goal (0-100):
Goal: {_goal}
Tool: {config.Name}
Description: {config.Description}
Settings: timeout={config.TimeoutSeconds}s, retries={config.MaxRetries}, cache={config.CacheResults}

Just respond with a number 0-100.";

        try
        {
            string response = await _llm.InnerModel.GenerateTextAsync(prompt);
            var match = DigitsRegex().Match(response);
            if (match.Success && int.TryParse(match.Value, out int llmScore))
            {
                return llmScore / 100.0;
            }
        }
        catch (Exception)
        {
            // LLM evaluation failed — fall through to heuristic
        }

        // Heuristic fallback scoring
        double heuristicScore = 0.5;

        // Prefer caching for repeated queries
        if (config.CacheResults) heuristicScore += 0.1;

        // Reasonable timeout
        if (config.TimeoutSeconds is >= 20 and <= 45) heuristicScore += 0.1;

        // Good retry count
        if (config.MaxRetries is >= 2 and <= 4) heuristicScore += 0.1;

        return Math.Min(heuristicScore, 1.0);
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();
}