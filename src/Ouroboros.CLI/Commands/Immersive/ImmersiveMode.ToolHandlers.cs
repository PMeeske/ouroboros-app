// <copyright file="ImmersiveMode.ToolHandlers.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

/// <summary>
/// Tool action handlers: adding, creating, searching, listing tools and self-modification help.
/// Also includes tool creation context detection for conversational flow.
/// </summary>
public sealed partial class ImmersiveMode
{
    private async Task<string> HandleAddToolAsync(
        string toolName,
        string personaName,
        CancellationToken ct)
    {
        if (_tools.DynamicToolFactory == null)
            return "Tool creation is not available.";

        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent($"[~] Creating tool: {toolName}...")}");

        try
        {
            // Create tool based on name hints for known patterns
            ITool? newTool = toolName.ToLowerInvariant() switch
            {
                var n when n.Contains("search") || n.Contains("google") || n.Contains("web") =>
                    _tools.DynamicToolFactory.CreateWebSearchTool("duckduckgo"),
                var n when n.Contains("fetch") || n.Contains("url") || n.Contains("http") =>
                    _tools.DynamicToolFactory.CreateUrlFetchTool(),
                var n when n.Contains("calc") || n.Contains("math") =>
                    _tools.DynamicToolFactory.CreateCalculatorTool(),
                _ => null // Unknown type - will try LLM generation
            };

            if (newTool != null)
            {
                _tools.DynamicTools = _tools.DynamicTools.WithTool(newTool);
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Created tool: {newTool.Name}")}");
                return $"I created a new {newTool.Name} tool. It's ready to use.";
            }

            // Unknown tool type - use LLM to generate it
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("[~] Using AI to generate custom tool...")}");
            var description = $"A tool named {toolName} that performs operations related to {toolName}";
            var createResult = await _tools.DynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _tools.DynamicTools = _tools.DynamicTools.WithTool(createResult.Value);
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Created custom tool: {createResult.Value.Name}")}");
                return $"I created a custom '{createResult.Value.Name}' tool using AI. It's ready to use.";
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]\\[!] AI tool generation failed: {Markup.Escape(createResult.Error)}[/]");
                return $"I couldn't create a '{toolName}' tool. Error: {createResult.Error}";
            }
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {ex.Message}")}[/]");
        }

        return $"I had trouble creating that tool. Try being more specific about what it should do.";
    }

    private async Task<string> HandleCreateToolFromDescriptionAsync(
        string description,
        string personaName,
        CancellationToken ct)
    {
        if (_tools.DynamicToolFactory == null)
            return "Tool creation is not available.";

        AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\\[~] Creating custom tool from description...[/]");
        AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("Description:")} {Markup.Escape(description)}");

        try
        {
            // Generate a tool name from the description
            var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .Take(3)
                .Select(w => char.ToUpper(w[0]) + w[1..].ToLower());
            var toolName = string.Join("", words) + "Tool";
            if (toolName.Length < 6) toolName = "CustomTool";

            var createResult = await _tools.DynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _tools.DynamicTools = _tools.DynamicTools.WithTool(createResult.Value);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] Created tool: {createResult.Value.Name}"));
                return $"Done! I created a '{createResult.Value.Name}' tool that {description}. It's ready to use.";
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {createResult.Error}")}[/]");
                return $"I couldn't create that tool. Error: {createResult.Error}";
            }
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {ex.Message}")}[/]");
            return $"Tool creation failed: {ex.Message}";
        }
    }

    private async Task<string> HandleCreateToolFromContextAsync(
        string topic,
        string description,
        string personaName,
        CancellationToken ct)
    {
        if (_tools.DynamicToolFactory == null)
            return "Tool creation is not available.";

        AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\\[~] Creating tool based on our conversation...[/]");
        AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("Topic:")} {Markup.Escape(topic)}");

        try
        {
            var toolName = topic.Replace(" ", "") + "Tool";
            var createResult = await _tools.DynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _tools.DynamicTools = _tools.DynamicTools.WithTool(createResult.Value);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] Created tool: {createResult.Value.Name}"));
                return $"Done! I created '{createResult.Value.Name}'. It's ready to use.";
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {createResult.Error}")}[/]");
                return $"I couldn't create that tool. {createResult.Error}";
            }
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {ex.Message}")}[/]");
            return $"Tool creation failed: {ex.Message}";
        }
    }

    private async Task<string> HandleSmartToolAsync(
        string goal,
        string personaName,
        CancellationToken ct)
    {
        if (_tools.ToolLearner == null)
            return "Intelligent tool discovery is not available.";

        AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\\[~] Finding best tool for: {Markup.Escape(goal)}...[/]");

        try
        {
            var result = await _tools.ToolLearner.FindOrCreateToolAsync(goal, _tools.DynamicTools, ct);
            if (result.IsSuccess)
            {
                var (tool, wasCreated) = result.Value;
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] {(wasCreated ? "Created" : "Found")} tool: {tool.Name}"));

                // Learn from tool usage (interconnected learning)
                if (_tools.InterconnectedLearner != null)
                {
                    await _tools.InterconnectedLearner.RecordToolExecutionAsync(
                        tool.Name,
                        goal,
                        $"Tool found for: {goal}",
                        true,
                        TimeSpan.Zero,
                        ct);
                }

                return $"I found the best tool for that: {tool.Name}.";
            }
            return $"I couldn't find a suitable tool for '{goal}'. {result.Error}";
        }
        catch (InvalidOperationException ex)
        {
            return $"Smart tool search failed: {ex.Message}";
        }
    }

    private string HandleToolStats(string personaName)
    {
        if (_tools.ToolLearner == null)
            return "Tool learning is not available in this session.";

        var stats = _tools.ToolLearner.GetStats();
        AnsiConsole.WriteLine();
        var statsTable = OuroborosTheme.ThemedTable("Metric", "Value");
        statsTable.AddRow(Markup.Escape("Total patterns"), Markup.Escape($"{stats.TotalPatterns}"));
        statsTable.AddRow(Markup.Escape("Success rate"), Markup.Escape($"{stats.AvgSuccessRate:P0}"));
        statsTable.AddRow(Markup.Escape("Total usage"), Markup.Escape($"{stats.TotalUsage}"));
        AnsiConsole.Write(OuroborosTheme.ThemedPanel(statsTable, "Tool Learning Stats"));

        return $"I've learned {stats.TotalPatterns} patterns with a {stats.AvgSuccessRate:P0} success rate. Total usage: {stats.TotalUsage}.";
    }

    private async Task<string> HandleConnectionsAsync(string personaName, CancellationToken ct)
    {
        if (_tools.InterconnectedLearner == null)
            return "Interconnected learning is not available in this session.";

        AnsiConsole.WriteLine();

        // Show stats from the learner
        var stats = _tools.InterconnectedLearner.GetStats();
        int totalExecutions = stats.TotalToolExecutions + stats.TotalSkillExecutions + stats.TotalPipelineExecutions;
        double successRate = totalExecutions > 0 ? (double)stats.SuccessfulExecutions / totalExecutions : 0;
        var connTable = OuroborosTheme.ThemedTable("Metric", "Value");
        connTable.AddRow(Markup.Escape("Patterns Learned"), Markup.Escape($"{stats.LearnedPatterns}"));
        connTable.AddRow(Markup.Escape("Concepts Mapped"), Markup.Escape($"{stats.ConceptGraphNodes}"));
        connTable.AddRow(Markup.Escape("Executions Recorded"), Markup.Escape($"{totalExecutions}"));
        connTable.AddRow(Markup.Escape("Avg Success Rate"), Markup.Escape($"{successRate:P0}"));
        AnsiConsole.Write(OuroborosTheme.ThemedPanel(connTable, "Interconnected Learning"));

        // Show sample suggestions
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent("Sample suggestions for common goals:")}");
        var sampleGoals = new[] { "search", "analyze", "summarize" };
        foreach (var goal in sampleGoals)
        {
            var suggestion = await _tools.InterconnectedLearner.SuggestForGoalAsync(goal, _tools.DynamicTools, ct);
            var actions = suggestion.MeTTaSuggestions.Concat(suggestion.RelatedConcepts).Take(3).ToList();
            if (actions.Count > 0)
            {
                AnsiConsole.MarkupLine($"    {OuroborosTheme.GoldText(goal)} -> \\[{Markup.Escape(string.Join(", ", actions))}]");
            }
        }

        return stats.LearnedPatterns > 0
            ? $"I have {stats.LearnedPatterns} learned patterns across {stats.ConceptGraphNodes} concepts. Use tools and skills to build more connections!"
            : "I haven't learned any patterns yet. Use skills and tools and I'll start learning relationships.";
    }

    private async Task<string> HandleGoogleSearchAsync(
        string query,
        string personaName,
        CancellationToken ct)
    {
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent($"[~] Searching Google for: {query}...")}");

        // Find the Google search tool
        var googleTool = _tools.DynamicTools.All
            .FirstOrDefault(t => t.Name.Contains("google", StringComparison.OrdinalIgnoreCase) ||
                                 t.Name.Contains("search", StringComparison.OrdinalIgnoreCase));

        if (googleTool == null)
        {
            return "Google search tool is not available. Try 'add tool search' first.";
        }

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await googleTool.InvokeAsync(query);
            stopwatch.Stop();

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK] Search complete")}");

            // Parse and display results
            var output = result.IsSuccess ? result.Value : "No results found.";
            if (output.Length > 500)
            {
                output = output[..500] + "...";
            }
            AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent("Results:")}");
            AnsiConsole.MarkupLine($"  {Markup.Escape(output.Replace("\n", "\n  "))}");

            // Learn from the search (interconnected learning)
            if (_tools.InterconnectedLearner != null)
            {
                await _tools.InterconnectedLearner.RecordToolExecutionAsync(
                    googleTool.Name,
                    query,
                    output,
                    true,
                    stopwatch.Elapsed,
                    ct);
            }

            return $"I found results for '{query}'. The search returned information about it.";
        }
        catch (HttpRequestException ex)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} \\[!] Search failed: {Markup.Escape(ex.Message)}[/]");
            return $"I couldn't complete the search. Error: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} \\[!] Search failed: {Markup.Escape(ex.Message)}[/]");
            return $"I couldn't complete the search. Error: {ex.Message}";
        }
    }

    private async Task<string> HandleUseToolAsync(string toolName, string toolInput, string personaName, CancellationToken ct)
    {
        if (_tools.DynamicTools == null)
            return "I don't have any tools loaded right now.";

        var tool = _tools.DynamicTools.Get(toolName);
        if (tool == null)
        {
            // Try to find a close match
            var availableTools = _tools.DynamicTools.All.Select(t => t.Name).ToList();
            var closestMatch = availableTools
                .OrderBy(t => LevenshteinDistance(t.ToLower(), toolName.ToLower()))
                .FirstOrDefault();

            return $"I don't have a tool called '{toolName}'. Did you mean '{closestMatch}'?\n\nAvailable tools include: {string.Join(", ", availableTools.Take(10))}";
        }

        // If no input provided, show the tool's usage
        if (string.IsNullOrWhiteSpace(toolInput) || toolInput == "{}")
        {
            // For tools that don't need input, execute directly
            if (string.IsNullOrEmpty(tool.JsonSchema) || tool.JsonSchema == "null")
            {
                toolInput = "{}";
            }
            else
            {
                return $"**Tool: {tool.Name}**\n\n{tool.Description}\n\n**Required input format:**\n```json\n{tool.JsonSchema ?? "{}"}\n```\n\nExample: `tool {toolName} {{\"param\": \"value\"}}`";
            }
        }

        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.GoldText($"[Executing tool: {toolName}...]")}");

        try
        {
            var result = await tool.InvokeAsync(toolInput, ct);
            return result.Match(
                success => $"**{toolName} result:**\n\n{success}",
                error => $"**{toolName} failed:**\n\n{error}"
            );
        }
        catch (InvalidOperationException ex)
        {
            return $"Tool execution error: {ex.Message}";
        }
    }

}
