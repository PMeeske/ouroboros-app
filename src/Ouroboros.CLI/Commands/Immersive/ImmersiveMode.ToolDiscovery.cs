// <copyright file="ImmersiveMode.ToolDiscovery.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Text;
using System.Text.RegularExpressions;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

/// <summary>
/// Tool listing, self-modification help, and tool creation context detection.
/// </summary>
public sealed partial class ImmersiveMode
{
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
            sb.AppendLine("  \U0001f9ec **Self-Modification:**");
            foreach (var t in selfTools.Take(8))
                sb.AppendLine($"    \u2022 `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (fileTools.Any())
        {
            sb.AppendLine("  \U0001f4c1 **File System:**");
            foreach (var t in fileTools.Take(6))
                sb.AppendLine($"    \u2022 `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (systemTools.Any())
        {
            sb.AppendLine("  \U0001f4bb **System:**");
            foreach (var t in systemTools.Take(6))
                sb.AppendLine($"    \u2022 `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (otherTools.Any())
        {
            sb.AppendLine("  \U0001f527 **Other:**");
            foreach (var t in otherTools.Take(8))
                sb.AppendLine($"    \u2022 `{t.Name}` - {Truncate(t.Description, 60)}");
        }

        sb.AppendLine("\n  **Usage:** `tool <name> {\"param\": \"value\"}`");

        AnsiConsole.MarkupLine(Markup.Escape(sb.ToString()));
        return $"I have {tools.Count} tools available. Key ones: {string.Join(", ", selfTools.Select(t => t.Name))}";
    }

    private string HandleSelfModificationHelp(string personaName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n  \U0001f9ec **Self-Modification Capabilities**\n");
        sb.AppendLine("  I can actually modify my own source code! Here's how:\n");
        sb.AppendLine("  1\ufe0f\u20e3 **Search my code:**");
        sb.AppendLine("     `tool search_my_code {\"query\": \"what to find\"}`\n");
        sb.AppendLine("  2\ufe0f\u20e3 **Read a file:**");
        sb.AppendLine("     `tool read_my_file {\"path\": \"src/Ouroboros.Cli/Commands/ImmersiveMode.cs\"}`\n");
        sb.AppendLine("  3\ufe0f\u20e3 **Modify code:**");
        sb.AppendLine("     `tool modify_my_code {\"file\": \"path/to/file.cs\", \"search\": \"old text\", \"replace\": \"new text\"}`\n");
        sb.AppendLine("  4\ufe0f\u20e3 **Create new tool:**");
        sb.AppendLine("     `tool create_new_tool {\"name\": \"my_tool\", \"description\": \"what it does\", \"implementation\": \"C# code\"}`\n");
        sb.AppendLine("  5\ufe0f\u20e3 **Rebuild myself:**");
        sb.AppendLine("     `rebuild` or `tool rebuild_self`\n");
        sb.AppendLine("  6\ufe0f\u20e3 **View/revert changes:**");
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
