// <copyright file="ImmersiveMode.Actions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Text.RegularExpressions;
using Ouroboros.Application;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.Options;
using Ouroboros.Providers.TextToSpeech;
using static Ouroboros.Application.Tools.AutonomousTools;

/// <summary>
/// Partial class containing action routing (TryHandleActionAsync),
/// small utility methods, and GeneratedRegex declarations for ImmersiveMode.
/// </summary>
public sealed partial class ImmersiveMode
{
    // ── Action routing ──────────────────────────────────────────────────────

    /// <summary>
    /// Try to handle skill, tool, or pipeline action commands.
    /// Returns null if not an action command, otherwise returns the result message.
    /// </summary>
    private async Task<string?> TryHandleActionAsync(
        string input,
        ImmersivePersona persona,
        ITextToSpeechService? tts,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        var lower = input.ToLowerInvariant().Trim();

        // List skills
        if (lower is "list skills" or "what skills" or "show skills" or "skills")
        {
            return await HandleListSkillsAsync(personaName);
        }

        // List tokens
        if (lower is "tokens" or "list tokens" or "show tokens" or "pipeline tokens")
        {
            return HandleListTokens(personaName);
        }

        // Pipeline help/examples
        if (lower is "pipeline help" or "pipeline examples" or "help pipeline" or "how to use pipeline" or "pipeline usage")
        {
            return HandlePipelineHelp(personaName);
        }

        // Tool stats
        if (lower is "tool stats" or "toolstats" or "show tool stats")
        {
            return HandleToolStats(personaName);
        }

        // Interconnected learning connections
        if (lower is "connections" or "show connections" or "learning connections" or "tool skill connections")
        {
            return await HandleConnectionsAsync(personaName, ct);
        }

        // Run skill: "run <skillname>" or "execute <skillname>"
        var runMatch = RunExecuteRegex().Match(lower);
        if (runMatch.Success)
        {
            var skillName = runMatch.Groups[2].Value.Trim();
            return await HandleRunSkillAsync(skillName, personaName, options, ct);
        }

        // Google search: "google <query>" or "google search <query>"
        var googleMatch = GoogleSearchRegex().Match(lower);
        if (googleMatch.Success)
        {
            var query = googleMatch.Groups[2].Value.Trim();
            return await HandleGoogleSearchAsync(query, personaName, ct);
        }

        // Learn about topic: "learn about <topic>"
        var learnMatch = LearnAboutRegex().Match(lower);
        if (learnMatch.Success)
        {
            var topic = learnMatch.Groups[1].Value.Trim();
            return await HandleLearnAboutAsync(topic, personaName, options, ct);
        }

        // Conversational tool creation confirmation: "yes", "ok", "create it", "do it", "build it"
        if (_pendingToolRequest != null && ConfirmationRegex().IsMatch(lower))
        {
            var (topic, description) = _pendingToolRequest.Value;
            _pendingToolRequest = null;
            return await HandleCreateToolFromContextAsync(topic, description, personaName, ct);
        }

        // Add tool: "add tool <name>" or "learn <toolname>" or "create tool <name>"
        var toolMatch = AddToolRegex().Match(lower);
        if (toolMatch.Success && !lower.Contains("about"))
        {
            var toolName = toolMatch.Groups[2].Value.Trim();
            return await HandleAddToolAsync(toolName, personaName, ct);
        }

        // Natural language tool creation: "create a tool that...", "build a tool for...", "make a tool to..."
        var nlToolMatch = NaturalLanguageToolRegex().Match(lower);
        if (nlToolMatch.Success)
        {
            var description = nlToolMatch.Groups[4].Value.Trim();
            return await HandleCreateToolFromDescriptionAsync(description, personaName, ct);
        }

        // Smart tool: "smart tool for <goal>" or "find tool for <goal>"
        var smartMatch = SmartToolRegex().Match(lower);
        if (smartMatch.Success)
        {
            var goal = smartMatch.Groups[3].Value.Trim();
            return await HandleSmartToolAsync(goal, personaName, ct);
        }

        // Memory recall: "remember <topic>", "recall <topic>", "what do you remember about <topic>"
        var rememberMatch = MemoryRecallRegex().Match(lower);
        if (rememberMatch.Success)
        {
            var topic = rememberMatch.Groups[2].Value.Trim();
            return await HandleMemoryRecallAsync(topic, personaName, ct);
        }

        // Memory stats: "memory stats", "conversation history"
        if (lower is "memory stats" or "conversation history" or "my memory" or "your memory")
        {
            return await HandleMemoryStatsAsync(personaName, ct);
        }

        // Autonomous mind commands
        if (lower is "mind state" or "mind status" or "autonomous state" or "your mind")
        {
            return HandleMindState();
        }

        if (lower is "start mind" or "start thinking" or "wake up mind" or "enable autonomous")
        {
            _autonomousMind?.Start();
            return "\ud83e\udde0 Autonomous mind activated. I'll think, explore the internet, and learn in the background.";
        }

        if (lower is "stop mind" or "stop thinking" or "pause mind" or "disable autonomous")
        {
            if (_autonomousMind != null)
            {
                await _autonomousMind.StopAsync();
            }
            return "\ud83d\udca4 Autonomous mind paused. I'll only respond when you talk to me.";
        }

        if (lower is "interests" or "my interests" or "what are you curious about")
        {
            return HandleShowInterests();
        }

        var thinkAboutMatch = ThinkAboutRegex().Match(lower);
        if (thinkAboutMatch.Success)
        {
            var topic = thinkAboutMatch.Groups[2].Value.Trim();
            _autonomousMind?.InjectTopic(topic);
            _autonomousMind?.AddInterest(topic);
            return $"\ud83e\udd14 I'll explore '{topic}' in the background and let you know if I find something interesting!";
        }

        var addInterestMatch = AddInterestRegex().Match(lower);
        if (addInterestMatch.Success)
        {
            var interest = addInterestMatch.Groups[2].Value.Trim();
            _autonomousMind?.AddInterest(interest);
            return $"\ud83d\udccc Added '{interest}' to my interests. I'll keep an eye out for related information!";
        }

        // Reindex commands: "reindex", "reindex full", "reindex incremental"
        if (lower == "reindex" || lower == "reindex full")
        {
            return await HandleFullReindexAsync(personaName, ct);
        }

        if (lower == "reindex incremental" || lower == "reindex inc")
        {
            return await HandleIncrementalReindexAsync(personaName, ct);
        }

        // Index search: "index search <query>" or "search index <query>"
        var indexSearchMatch = IndexSearchRegex().Match(lower);
        if (indexSearchMatch.Success)
        {
            var query = indexSearchMatch.Groups[2].Value.Trim();
            return await HandleIndexSearchAsync(query, personaName, ct);
        }

        // Index stats: "index stats" or "indexer stats"
        if (lower == "index stats" || lower == "indexer stats")
        {
            return await HandleIndexStatsAsync(personaName, ct);
        }

        // Emergence: "emergence <topic>"
        var emergenceMatch = EmergenceRegex().Match(lower);
        if (emergenceMatch.Success)
        {
            var topic = emergenceMatch.Groups[1].Value.Trim();
            return await HandleEmergenceAsync(topic, personaName, options, ct);
        }

        // Pipeline DSL: contains pipe symbol
        if (input.Contains('|'))
        {
            return await HandlePipelineAsync(input, personaName, options, ct);
        }

        // Try to match single pipeline token by name (e.g., "ArxivSearch transformers")
        var singleTokenResult = await TryExecuteSingleTokenAsync(input, personaName, ct);
        if (singleTokenResult != null)
        {
            return singleTokenResult;
        }

        // Try natural language patterns for common tokens
        var nlResult = await TryNaturalLanguageTokenAsync(input, personaName, ct);
        if (nlResult != null)
        {
            return nlResult;
        }

        // Use tool command: "use tool <name> <json_input>" or "tool <name> <json_input>"
        var useToolMatch = UseToolRegex().Match(lower);
        if (useToolMatch.Success)
        {
            var toolName = useToolMatch.Groups[1].Value.Trim();
            var toolInput = useToolMatch.Groups[2].Value.Trim();
            return await HandleUseToolAsync(toolName, toolInput, personaName, ct);
        }

        // List available tools: "list tools" or "what tools" or "show tools"
        if (lower is "list tools" or "what tools" or "show tools" or "tools" or "my tools" or "available tools")
        {
            return HandleListTools(personaName);
        }

        // Self-modification natural language triggers
        if (lower.Contains("modify") && (lower.Contains("your code") || lower.Contains("yourself") || lower.Contains("my code")))
        {
            return HandleSelfModificationHelp(personaName);
        }

        if (lower is "rebuild" or "rebuild yourself" or "recompile" or "build yourself")
        {
            return await HandleUseToolAsync("rebuild_self", "{}", personaName, ct);
        }

        if (lower is "modification history" or "my modifications" or "what did i change" or "view changes")
        {
            return await HandleUseToolAsync("view_modification_history", "{}", personaName, ct);
        }

        // Not an action command
        return null;
    }

    // ── Small utility methods ───────────────────────────────────────────────

    private static bool IsExitCommand(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        return lower is "exit" or "quit" or "bye" or "goodbye" or "leave" or "stop" or "end";
    }

    private static string NormalizeEndpoint(string? rawEndpoint, string fallbackEndpoint)
    {
        var endpoint = (rawEndpoint ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return fallbackEndpoint;
        }

        if (!endpoint.Contains("://", StringComparison.Ordinal))
        {
            endpoint = $"http://{endpoint}";
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            return fallbackEndpoint;
        }

        return uri.ToString().TrimEnd('/');
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[s.Length, t.Length];
    }

    // Causal extraction and graph building consolidated in SharedAgentBootstrap.
    // Call sites use SharedAgentBootstrap.TryExtractCausalTerms / BuildMinimalCausalGraph.

    // ── GeneratedRegex declarations ─────────────────────────────────────────

    [GeneratedRegex(@"^(run|execute)\s+(.+)$")]
    private static partial Regex RunExecuteRegex();

    [GeneratedRegex(@"^google\s*(search)?\s*(.+)$")]
    private static partial Regex GoogleSearchRegex();

    [GeneratedRegex(@"^learn\s+about\s+(.+)$")]
    private static partial Regex LearnAboutRegex();

    [GeneratedRegex(@"^(yes|ok|create\s*it|do\s*it|build\s*it|make\s*it|go\s*ahead|sure|please)$")]
    private static partial Regex ConfirmationRegex();

    [GeneratedRegex(@"^(add\s+tool|learn|create\s+tool|build\s+tool|make\s+tool)\s+(.+)$")]
    private static partial Regex AddToolRegex();

    [GeneratedRegex(@"^(create|build|make)\s+(a\s+)?tool\s+(that|for|to|which)\s+(.+)$")]
    private static partial Regex NaturalLanguageToolRegex();

    [GeneratedRegex(@"^(smart\s+tool|find\s+tool)\s+(for\s+)?(.+)$")]
    private static partial Regex SmartToolRegex();

    [GeneratedRegex(@"^(remember|recall|what\s+do\s+you\s+remember\s+about)\s+(.+)$")]
    private static partial Regex MemoryRecallRegex();

    [GeneratedRegex(@"^(think about|explore|be curious about|research)\s+(.+)$")]
    private static partial Regex ThinkAboutRegex();

    [GeneratedRegex(@"^(add interest|interest in|i'm interested in)\s+(.+)$")]
    private static partial Regex AddInterestRegex();

    [GeneratedRegex(@"^(index\s+search|search\s+index|find\s+in\s+index)\s+(.+)$")]
    private static partial Regex IndexSearchRegex();

    [GeneratedRegex(@"^emergence\s+(.+)$")]
    private static partial Regex EmergenceRegex();

    [GeneratedRegex(@"^(?:use\s+)?tool\s+(\w+)\s*(.*)$")]
    private static partial Regex UseToolRegex();
}
