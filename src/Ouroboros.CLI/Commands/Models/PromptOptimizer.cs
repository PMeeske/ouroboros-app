using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Runtime prompt optimizer that learns from interaction outcomes.
/// Uses multi-armed bandit approach to balance exploration vs exploitation.
/// </summary>
public sealed class PromptOptimizer
{
    private readonly ConcurrentDictionary<string, PromptPattern> _patterns = new();
    private readonly ConcurrentQueue<InteractionOutcome> _recentOutcomes = new();
    private readonly Random _random = new();
    private const int MaxOutcomeHistory = 100;
    private const double ExplorationRate = 0.15; // 15% exploration

    // Learned effectiveness weights
    private double _toolSyntaxEmphasisWeight = 1.0;
    private double _exampleDensityWeight = 1.0;
    private double _warningEmphasisWeight = 1.0;
    private double _contextInjectionWeight = 1.0;

    public PromptOptimizer()
    {
        InitializeDefaultPatterns();
    }

    private void InitializeDefaultPatterns()
    {
        // Different instruction styles to test
        _patterns["tool_syntax_basic"] = new PromptPattern
        {
            Name = "Basic Tool Syntax",
            Template = "To use a tool, write [TOOL:toolname input]"
        };

        _patterns["tool_syntax_emphatic"] = new PromptPattern
        {
            Name = "Emphatic Tool Syntax",
            Template = "⚠️ CRITICAL: You MUST use exact syntax [TOOL:toolname input] - no exceptions!"
        };

        _patterns["tool_syntax_example_heavy"] = new PromptPattern
        {
            Name = "Example-Heavy Tool Syntax",
            Template = @"Tool syntax: [TOOL:name args]
Example 1: [TOOL:search_my_code WorldModel]
Example 2: [TOOL:read_my_file src/file.cs]
Example 3: [TOOL:calculator 2+2]
ALWAYS follow this exact pattern."
        };

        _patterns["mandatory_rule"] = new PromptPattern
        {
            Name = "Mandatory Rule Pattern",
            Template = @"MANDATORY: Questions about code/architecture REQUIRE tool usage.
NEVER answer from memory. ALWAYS [TOOL:search_my_code X] first."
        };

        _patterns["action_trigger_sparse"] = new PromptPattern
        {
            Name = "Sparse Action Triggers",
            Template = "'search X' → [TOOL:search_my_code X]"
        };

        _patterns["action_trigger_detailed"] = new PromptPattern
        {
            Name = "Detailed Action Triggers",
            Template = @"TRIGGERS: When user says 'search/find/look for X' you MUST output [TOOL:search_my_code X]
When user asks 'is there a X' you MUST output [TOOL:search_my_code X]
When user says 'read file X' you MUST output [TOOL:read_my_file X]"
        };
    }

    /// <summary>
    /// Records an interaction outcome for learning.
    /// </summary>
    public void RecordOutcome(InteractionOutcome outcome)
    {
        _recentOutcomes.Enqueue(outcome);
        while (_recentOutcomes.Count > MaxOutcomeHistory)
            _recentOutcomes.TryDequeue(out _);

        // Update pattern statistics based on what was in the prompt
        foreach (var pattern in _patterns.Values.Where(p => p.LastUsed > DateTime.UtcNow.AddMinutes(-5)))
        {
            pattern.UsageCount++;
            if (outcome.WasSuccessful)
            {
                pattern.SuccessCount++;
                if (outcome.ActualToolCalls.Count > 0)
                    pattern.SuccessfulVariants.Add(string.Join(",", outcome.ActualToolCalls));
            }
            else
            {
                pattern.FailureCount++;
                pattern.FailedVariants.Add($"Expected: {string.Join(",", outcome.ExpectedTools)} Got: {string.Join(",", outcome.ActualToolCalls)}");
            }
        }

        // Adaptive weight learning
        UpdateWeights(outcome);
    }

    private void UpdateWeights(InteractionOutcome outcome)
    {
        double learningRate = 0.1;

        // If tools were called successfully, increase relevant weights
        if (outcome.ActualToolCalls.Count > 0 && outcome.WasSuccessful)
        {
            _toolSyntaxEmphasisWeight = Math.Min(2.0, _toolSyntaxEmphasisWeight + learningRate);
        }
        // If tools should have been called but weren't, we need stronger emphasis
        else if (outcome.ExpectedTools.Count > 0 && outcome.ActualToolCalls.Count == 0)
        {
            _warningEmphasisWeight = Math.Min(3.0, _warningEmphasisWeight + learningRate * 2);
            _exampleDensityWeight = Math.Min(2.0, _exampleDensityWeight + learningRate);
        }
    }

    /// <summary>
    /// Selects the best prompt variant using Thompson Sampling.
    /// </summary>
    public PromptPattern SelectBestPattern(string category)
    {
        var relevantPatterns = _patterns.Values
            .Where(p => p.Name.Contains(category, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (relevantPatterns.Count == 0)
            return _patterns.Values.First();

        // Exploration: random selection
        if (_random.NextDouble() < ExplorationRate)
        {
            var selected = relevantPatterns[_random.Next(relevantPatterns.Count)];
            selected.LastUsed = DateTime.UtcNow;
            return selected;
        }

        // Exploitation: Thompson Sampling using Beta distribution
        var bestPattern = relevantPatterns
            .Select(p => new
            {
                Pattern = p,
                // Sample from Beta(success+1, failure+1)
                Score = SampleBeta(p.SuccessCount + 1, p.FailureCount + 1)
            })
            .OrderByDescending(x => x.Score)
            .First()
            .Pattern;

        bestPattern.LastUsed = DateTime.UtcNow;
        return bestPattern;
    }

    private double SampleBeta(int alpha, int beta)
    {
        // Approximation of Beta distribution sampling
        double x = SampleGamma(alpha);
        double y = SampleGamma(beta);
        return x / (x + y);
    }

    private double SampleGamma(int shape)
    {
        // Marsaglia and Tsang's method approximation
        if (shape < 1) return SampleGamma(shape + 1) * Math.Pow(_random.NextDouble(), 1.0 / shape);

        double d = shape - 1.0 / 3.0;
        double c = 1.0 / Math.Sqrt(9.0 * d);

        while (true)
        {
            double x, v;
            do
            {
                x = NextGaussian();
                v = 1.0 + c * x;
            } while (v <= 0);

            v = v * v * v;
            double u = _random.NextDouble();

            if (u < 1 - 0.0331 * (x * x) * (x * x)) return d * v;
            if (Math.Log(u) < 0.5 * x * x + d * (1 - v + Math.Log(v))) return d * v;
        }
    }

    private double NextGaussian()
    {
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    /// <summary>
    /// Generates an optimized tool instruction based on learned patterns.
    /// </summary>
    public string GenerateOptimizedToolInstruction(List<string> availableTools, string userInput)
    {
        var sb = new StringBuilder();

        // Select best patterns for each category
        var syntaxPattern = SelectBestPattern("syntax");
        var mandatoryPattern = SelectBestPattern("mandatory");
        var triggerPattern = SelectBestPattern("trigger");

        // Apply learned weights
        if (_warningEmphasisWeight > 1.5)
        {
            sb.AppendLine("🚨 ABSOLUTE REQUIREMENT: USE TOOLS, DON'T JUST TALK ABOUT THEM 🚨");
        }

        sb.AppendLine();
        sb.AppendLine(syntaxPattern.Template);
        sb.AppendLine();

        if (_exampleDensityWeight > 1.3)
        {
            // Add more examples when learning shows they help
            sb.AppendLine("CONCRETE EXAMPLES (use these patterns exactly):");
            foreach (var tool in availableTools.Take(5))
            {
                sb.AppendLine($"  [TOOL:{tool} your_input_here]");
            }
            sb.AppendLine();
        }

        sb.AppendLine(mandatoryPattern.Template);
        sb.AppendLine();
        sb.AppendLine(triggerPattern.Template);

        // Add dynamic anti-pattern based on recent failures
        var recentFailures = _recentOutcomes
            .Where(o => !o.WasSuccessful && o.ExpectedTools.Count > 0)
            .TakeLast(3)
            .ToList();

        if (recentFailures.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("❌ RECENT MISTAKES TO AVOID:");
            foreach (var failure in recentFailures)
            {
                sb.AppendLine($"  - User asked '{failure.UserInput.Substring(0, Math.Min(50, failure.UserInput.Length))}...' but you didn't call {string.Join(", ", failure.ExpectedTools)}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets optimization statistics for introspection.
    /// </summary>
    public string GetStatistics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Prompt Optimization Statistics ===");
        sb.AppendLine($"Total interactions tracked: {_recentOutcomes.Count}");
        sb.AppendLine($"Learned weights:");
        sb.AppendLine($"  Tool Syntax Emphasis: {_toolSyntaxEmphasisWeight:F2}");
        sb.AppendLine($"  Example Density: {_exampleDensityWeight:F2}");
        sb.AppendLine($"  Warning Emphasis: {_warningEmphasisWeight:F2}");
        sb.AppendLine($"  Context Injection: {_contextInjectionWeight:F2}");
        sb.AppendLine();
        sb.AppendLine("Pattern Performance:");
        foreach (var pattern in _patterns.Values.OrderByDescending(p => p.SuccessRate))
        {
            sb.AppendLine($"  {pattern.Name}: {pattern.SuccessRate:P0} ({pattern.SuccessCount}/{pattern.UsageCount})");
        }

        var successRate = _recentOutcomes.Count > 0
            ? (double)_recentOutcomes.Count(o => o.WasSuccessful) / _recentOutcomes.Count
            : 0;
        sb.AppendLine();
        sb.AppendLine($"Overall Success Rate: {successRate:P0}");

        return sb.ToString();
    }

    /// <summary>
    /// Detects expected tools based on user input patterns.
    /// </summary>
    public List<string> DetectExpectedTools(string userInput)
    {
        var expected = new List<string>();
        var inputLower = userInput.ToLowerInvariant();

        if (inputLower.Contains("search") || inputLower.Contains("find") || inputLower.Contains("is there"))
            expected.Add("search_my_code");
        if (inputLower.Contains("read") || inputLower.Contains("show") || inputLower.Contains("cat "))
            expected.Add("read_my_file");
        if (inputLower.Contains("modify") || inputLower.Contains("change") || inputLower.Contains("edit") || inputLower.Contains("save"))
            expected.Add("modify_my_code");
        if (inputLower.Contains("calculate") || inputLower.Contains("math") || Regex.IsMatch(inputLower, @"\d+\s*[+\-*/]\s*\d+"))
            expected.Add("calculator");
        if (inputLower.Contains("web") || inputLower.Contains("search online") || inputLower.Contains("look up"))
            expected.Add("web_research");
        if (inputLower.Contains("world model") || inputLower.Contains("architecture") || inputLower.Contains("how does"))
            expected.Add("search_my_code");

        return expected;
    }

    /// <summary>
    /// Extracts actual tool calls from agent response.
    /// </summary>
    public List<string> ExtractToolCalls(string response)
    {
        var calls = new List<string>();
        var matches = Regex.Matches(response, @"\[TOOL:([^\s\]]+)");
        foreach (Match match in matches)
        {
            calls.Add(match.Groups[1].Value);
        }
        return calls;
    }
}