// <copyright file="ImmersiveMode.ToolHandlers.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.CLI.Commands;

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
        if (_dynamicToolFactory == null)
            return "Tool creation is not available.";

        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent($"[~] Creating tool: {toolName}...")}");

        try
        {
            // Create tool based on name hints for known patterns
            ITool? newTool = toolName.ToLowerInvariant() switch
            {
                var n when n.Contains("search") || n.Contains("google") || n.Contains("web") =>
                    _dynamicToolFactory.CreateWebSearchTool("duckduckgo"),
                var n when n.Contains("fetch") || n.Contains("url") || n.Contains("http") =>
                    _dynamicToolFactory.CreateUrlFetchTool(),
                var n when n.Contains("calc") || n.Contains("math") =>
                    _dynamicToolFactory.CreateCalculatorTool(),
                _ => null // Unknown type - will try LLM generation
            };

            if (newTool != null)
            {
                _dynamicTools = _dynamicTools.WithTool(newTool);
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Created tool: {newTool.Name}")}");
                return $"I created a new {newTool.Name} tool. It's ready to use.";
            }

            // Unknown tool type - use LLM to generate it
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("[~] Using AI to generate custom tool...")}");
            var description = $"A tool named {toolName} that performs operations related to {toolName}";
            var createResult = await _dynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _dynamicTools = _dynamicTools.WithTool(createResult.Value);
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"[OK] Created custom tool: {createResult.Value.Name}")}");
                return $"I created a custom '{createResult.Value.Name}' tool using AI. It's ready to use.";
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]\\[!] AI tool generation failed: {Markup.Escape(createResult.Error)}[/]");
                return $"I couldn't create a '{toolName}' tool. Error: {createResult.Error}";
            }
        }
        catch (Exception ex)
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
        if (_dynamicToolFactory == null)
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

            var createResult = await _dynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _dynamicTools = _dynamicTools.WithTool(createResult.Value);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] Created tool: {createResult.Value.Name}"));
                return $"Done! I created a '{createResult.Value.Name}' tool that {description}. It's ready to use.";
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {createResult.Error}")}[/]");
                return $"I couldn't create that tool. Error: {createResult.Error}";
            }
        }
        catch (Exception ex)
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
        if (_dynamicToolFactory == null)
            return "Tool creation is not available.";

        AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\\[~] Creating tool based on our conversation...[/]");
        AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("Topic:")} {Markup.Escape(topic)}");

        try
        {
            var toolName = topic.Replace(" ", "") + "Tool";
            var createResult = await _dynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _dynamicTools = _dynamicTools.WithTool(createResult.Value);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] Created tool: {createResult.Value.Name}"));
                return $"Done! I created '{createResult.Value.Name}'. It's ready to use.";
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Tool creation failed: {createResult.Error}")}[/]");
                return $"I couldn't create that tool. {createResult.Error}";
            }
        }
        catch (Exception ex)
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
        if (_toolLearner == null)
            return "Intelligent tool discovery is not available.";

        AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]\\[~] Finding best tool for: {Markup.Escape(goal)}...[/]");

        try
        {
            var result = await _toolLearner.FindOrCreateToolAsync(goal, _dynamicTools, ct);
            if (result.IsSuccess)
            {
                var (tool, wasCreated) = result.Value;
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] {(wasCreated ? "Created" : "Found")} tool: {tool.Name}"));

                // Learn from tool usage (interconnected learning)
                if (_interconnectedLearner != null)
                {
                    await _interconnectedLearner.RecordToolExecutionAsync(
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
        catch (Exception ex)
        {
            return $"Smart tool search failed: {ex.Message}";
        }
    }

    private string HandleToolStats(string personaName)
    {
        if (_toolLearner == null)
            return "Tool learning is not available in this session.";

        var stats = _toolLearner.GetStats();
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
        if (_interconnectedLearner == null)
            return "Interconnected learning is not available in this session.";

        AnsiConsole.WriteLine();

        // Show stats from the learner
        var stats = _interconnectedLearner.GetStats();
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
            var suggestion = await _interconnectedLearner.SuggestForGoalAsync(goal, _dynamicTools, ct);
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
        var googleTool = _dynamicTools.All
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
            if (_interconnectedLearner != null)
            {
                await _interconnectedLearner.RecordToolExecutionAsync(
                    googleTool.Name,
                    query,
                    output,
                    true,
                    stopwatch.Elapsed,
                    ct);
            }

            return $"I found results for '{query}'. The search returned information about it.";
        }
        catch (Exception ex)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} \\[!] Search failed: {Markup.Escape(ex.Message)}[/]");
            return $"I couldn't complete the search. Error: {ex.Message}";
        }
    }

    private async Task<string> HandleUseToolAsync(string toolName, string toolInput, string personaName, CancellationToken ct)
    {
        if (_dynamicTools == null)
            return "I don't have any tools loaded right now.";

        var tool = _dynamicTools.Get(toolName);
        if (tool == null)
        {
            // Try to find a close match
            var availableTools = _dynamicTools.All.Select(t => t.Name).ToList();
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
        catch (Exception ex)
        {
            return $"Tool execution error: {ex.Message}";
        }
    }

    private string HandleListTools(string personaName)
    {
        if (_dynamicTools == null)
            return "I don't have any tools loaded.";

        var tools = _dynamicTools.All.ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"\n  **My Tools ({tools.Count} available)**\n");

        // Group by category
        var selfTools = tools.Where(t => t.Name.Contains("my_") || t.Name.Contains("self") || t.Name.Contains("rebuild")).ToList();
        var fileTools = tools.Where(t => t.Name.Contains("file") || t.Name.Contains("directory")).ToList();
        var systemTools = tools.Where(t => t.Name.Contains("process") || t.Name.Contains("system") || t.Name.Contains("powershell")).ToList();
        var otherTools = tools.Except(selfTools).Except(fileTools).Except(systemTools).ToList();

        if (selfTools.Any())
        {
            sb.AppendLine("  🧬 **Self-Modification:**");
            foreach (var t in selfTools.Take(8))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (fileTools.Any())
        {
            sb.AppendLine("  📁 **File System:**");
            foreach (var t in fileTools.Take(6))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (systemTools.Any())
        {
            sb.AppendLine("  💻 **System:**");
            foreach (var t in systemTools.Take(6))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (otherTools.Any())
        {
            sb.AppendLine("  🔧 **Other:**");
            foreach (var t in otherTools.Take(8))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
        }

        sb.AppendLine("\n  **Usage:** `tool <name> {\"param\": \"value\"}`");

        AnsiConsole.MarkupLine(Markup.Escape(sb.ToString()));
        return $"I have {tools.Count} tools available. Key ones: {string.Join(", ", selfTools.Select(t => t.Name))}";
    }

    private string HandleSelfModificationHelp(string personaName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n  🧬 **Self-Modification Capabilities**\n");
        sb.AppendLine("  I can actually modify my own source code! Here's how:\n");
        sb.AppendLine("  1️⃣ **Search my code:**");
        sb.AppendLine("     `tool search_my_code {\"query\": \"what to find\"}`\n");
        sb.AppendLine("  2️⃣ **Read a file:**");
        sb.AppendLine("     `tool read_my_file {\"path\": \"src/Ouroboros.Cli/Commands/ImmersiveMode.cs\"}`\n");
        sb.AppendLine("  3️⃣ **Modify code:**");
        sb.AppendLine("     `tool modify_my_code {\"file\": \"path/to/file.cs\", \"search\": \"old text\", \"replace\": \"new text\"}`\n");
        sb.AppendLine("  4️⃣ **Create new tool:**");
        sb.AppendLine("     `tool create_new_tool {\"name\": \"my_tool\", \"description\": \"what it does\", \"implementation\": \"C# code\"}`\n");
        sb.AppendLine("  5️⃣ **Rebuild myself:**");
        sb.AppendLine("     `rebuild` or `tool rebuild_self`\n");
        sb.AppendLine("  6️⃣ **View/revert changes:**");
        sb.AppendLine("     `modification history` or `tool revert_modification {\"backup\": \"filename.backup\"}`");

        AnsiConsole.MarkupLine(Markup.Escape(sb.ToString()));
        return "Yes, I can modify myself! Use the commands above. Changes create automatic backups.";
    }

    /// <summary>
    /// Detects when conversation is about tool creation and sets pending context.
    /// This enables conversational flow: "Can you create a tool that X?" "Yes" -> creates tool.
    /// </summary>
    private void DetectToolCreationContext(string userInput, string aiResponse)
    {
        var lowerInput = userInput.ToLowerInvariant();
        var lowerResponse = aiResponse.ToLowerInvariant();

        // Patterns indicating user wants to create a tool
        var toolCreationPatterns = new[]
        {
            @"(can you|could you|would you|please)?\s*(create|build|make)\s*(a|me)?\s*tool",
            @"(i need|i want)\s*(a|you to make)?\s*tool",
            @"(create|build|make)\s*(me)?\s*(a|an)?\s*\w+\s*tool",
            @"tool\s*(that|to|for|which)\s+(.+)",
            @"(can|could)\s+you\s+(help me )?(create|build|make)",
        };

        // Check if user is asking about tool creation
        foreach (var pattern in toolCreationPatterns)
        {
            var match = Regex.Match(lowerInput, pattern);
            if (match.Success)
            {
                // Extract the tool purpose from the input
                var descriptionMatch = ToolPurposeRegex().Match(lowerInput);
                var description = descriptionMatch.Success
                    ? descriptionMatch.Groups[2].Value.Trim()
                    : userInput;

                // Try to extract a topic name
                var topicMatch = ToolTopicRegex().Match(lowerInput);
                var topic = topicMatch.Success
                    ? topicMatch.Groups[1].Value.Trim()
                    : ExtractTopicFromDescription(description);

                _pendingToolRequest = (topic, description);

                AnsiConsole.MarkupLine($"  [rgb(128,0,180)]\\[context] Tool creation detected: {Markup.Escape(topic)}[/]");
                AnsiConsole.MarkupLine($"  [rgb(128,0,180)]          Say 'yes', 'ok', or 'create it' to proceed.[/]");
                return;
            }
        }

        // Also detect when AI mentions it can/could create something
        if ((lowerResponse.Contains("i can create") || lowerResponse.Contains("i could create") ||
             lowerResponse.Contains("i'll create") || lowerResponse.Contains("i will create") ||
             lowerResponse.Contains("shall i create") || lowerResponse.Contains("want me to create")) &&
            (lowerResponse.Contains("tool") || lowerInput.Contains("tool")))
        {
            var topic = ExtractTopicFromDescription(userInput);
            _pendingToolRequest = (topic, userInput);

            AnsiConsole.MarkupLine($"  [rgb(128,0,180)]\\[context] Offering to create tool: {Markup.Escape(topic)}[/]");
            AnsiConsole.MarkupLine($"  [rgb(128,0,180)]          Say 'yes', 'ok', or 'create it' to proceed.[/]");
        }
    }

    /// <summary>
    /// Extracts a meaningful topic name from a description.
    /// </summary>
    private string ExtractTopicFromDescription(string description)
    {
        // Try to find meaningful words
        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Where(w => !new[] { "that", "this", "with", "from", "into", "about", "tool", "create", "make", "build", "would", "could", "should", "please" }.Contains(w.ToLower()))
            .Take(2)
            .Select(w => char.ToUpper(w[0]) + w[1..].ToLower());

        var topic = string.Join("", words);
        return string.IsNullOrEmpty(topic) ? "Custom" : topic;
    }

    [GeneratedRegex(@"tool\s+(that|to|for|which)\s+(.+)")]
    private static partial Regex ToolPurposeRegex();

    [GeneratedRegex(@"(?:create|build|make)\s+(?:a|an|me)?\s*(\w+)\s*tool")]
    private static partial Regex ToolTopicRegex();
}
