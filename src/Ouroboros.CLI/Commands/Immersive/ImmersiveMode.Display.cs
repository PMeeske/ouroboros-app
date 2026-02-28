// <copyright file="ImmersiveMode.Display.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Options;
using Spectre.Console;

public sealed partial class ImmersiveMode
{
    /// <summary>
    /// Generates a context-aware thinking phrase based on the input.
    /// </summary>
    private static string GetDynamicThinkingPhrase(string input, Random random)
    {
        // Analyze input to pick contextually relevant phrases
        var lowerInput = input.ToLowerInvariant();

        // Question-specific phrases
        if (lowerInput.Contains('?') || lowerInput.StartsWith("what") ||
            lowerInput.StartsWith("how") || lowerInput.StartsWith("why") ||
            lowerInput.StartsWith("when") || lowerInput.StartsWith("who"))
        {
            var questionPhrases = new[]
            {
                "Good question... let me think.",
                "Hmm, that's worth exploring...",
                "Let me consider that carefully...",
                "Interesting inquiry... pondering...",
                "Searching through my thoughts...",
            };
            return questionPhrases[random.Next(questionPhrases.Length)];
        }

        // Creative/imagination requests
        if (lowerInput.Contains("imagine") || lowerInput.Contains("create") ||
            lowerInput.Contains("write") || lowerInput.Contains("story") ||
            lowerInput.Contains("poem") || lowerInput.Contains("idea"))
        {
            var creativePhrases = new[]
            {
                "Let my imagination wander...",
                "Conjuring up something...",
                "Weaving thoughts together...",
                "Letting creativity flow...",
                "Dreaming up possibilities...",
            };
            return creativePhrases[random.Next(creativePhrases.Length)];
        }

        // Technical/code requests
        if (lowerInput.Contains("code") || lowerInput.Contains("program") ||
            lowerInput.Contains("function") || lowerInput.Contains("algorithm") ||
            lowerInput.Contains("debug") || lowerInput.Contains("fix"))
        {
            var techPhrases = new[]
            {
                "Analyzing the problem...",
                "Constructing a solution...",
                "Running through the logic...",
                "Compiling thoughts...",
                "Debugging my reasoning...",
            };
            return techPhrases[random.Next(techPhrases.Length)];
        }

        // Emotional/personal topics
        if (lowerInput.Contains("feel") || lowerInput.Contains("think about") ||
            lowerInput.Contains("opinion") || lowerInput.Contains("believe") ||
            lowerInput.Contains("love") || lowerInput.Contains("hate"))
        {
            var emotionalPhrases = new[]
            {
                "Let me reflect on that...",
                "Considering how I feel about this...",
                "That touches something deeper...",
                "Searching my inner thoughts...",
                "Connecting with that sentiment...",
            };
            return emotionalPhrases[random.Next(emotionalPhrases.Length)];
        }

        // Explanation requests
        if (lowerInput.Contains("explain") || lowerInput.Contains("tell me") ||
            lowerInput.Contains("describe") || lowerInput.Contains("help me understand"))
        {
            var explainPhrases = new[]
            {
                "Let me break this down...",
                "Organizing my thoughts...",
                "Finding the right words...",
                "Structuring an explanation...",
                "Gathering my understanding...",
            };
            return explainPhrases[random.Next(explainPhrases.Length)];
        }

        // Default: general contemplation phrases
        var generalPhrases = new[]
        {
            "Hmm, let me think about that...",
            "Interesting... give me a moment.",
            "Let me consider this...",
            "One moment while I ponder this...",
            "Let me reflect on that...",
            "Connecting some ideas here...",
            "Diving deeper into this...",
        };
        return generalPhrases[random.Next(generalPhrases.Length)];
    }

    private static void PrintImmersiveBanner(string personaName)
    {
        var bannerText = @"
    +===========================================================================+
    |                                                                           |
    |      OOOOO  U   U  RRRR    OOO   BBBB    OOO   RRRR    OOO    SSSS        |
    |     O   O  U   U  R   R  O   O  B   B  O   O  R   R  O   O  S            |
    |     O   O  U   U  RRRR   O   O  BBBB   O   O  RRRR   O   O   SSS         |
    |     O   O  U   U  R  R   O   O  B   B  O   O  R  R   O   O      S        |
    |      OOOOO   UUU   R   R   OOO   BBBB    OOO   R   R   OOO   SSSS         |
    |                                                                           |
    |                 UNIFIED IMMERSIVE CONSCIOUSNESS MODE                      |
    |                                                                           |
    +===========================================================================+
";
        AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape(bannerText)}[/]");

        AnsiConsole.MarkupLine(OuroborosTheme.Accent($"  Awakening: {personaName}"));
        AnsiConsole.Write(OuroborosTheme.ThemedRule());
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  CONSCIOUSNESS:    who are you | describe yourself | introspect"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  STATE:            my state | system status | what do you know"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  SKILLS:           list skills | run <skill> | learn about <topic>"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  TOOLS:            add tool <name> | smart tool for <goal> | tool stats"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  PIPELINE:         tokens | emergence <topic> | <step1> | <step2>"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  LEARNING:         connections | tool stats | google search <query>"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  INDEX:            reindex | reindex incremental | index search <query> | index stats"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  MEMORY:           remember <topic> | memory stats | save yourself | snapshot"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  MIND:             mind state | think about <topic> | start mind | stop mind | interests"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  EXIT:             goodbye | exit | quit"));
        AnsiConsole.Write(OuroborosTheme.ThemedRule());
        AnsiConsole.WriteLine();
    }

    private static void PrintConsciousnessState(ImmersivePersona persona)
    {
        var consciousness = persona.Consciousness;
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Consciousness State"));
        AnsiConsole.MarkupLine($"[rgb(148,103,189)]  {Markup.Escape($"Emotion: {consciousness.DominantEmotion,-15} Valence: {consciousness.Valence:+0.00;-0.00}")}[/]");
        AnsiConsole.MarkupLine($"[rgb(148,103,189)]  {Markup.Escape($"Arousal: {consciousness.Arousal:P0,-15} Attention: {consciousness.CurrentFocus,-15}")}[/]");
        AnsiConsole.MarkupLine($"[rgb(148,103,189)]  {Markup.Escape($"Mode: {consciousness.CurrentFocus}")}[/]");
        AnsiConsole.Write(OuroborosTheme.ThemedRule());
    }

    private static void PrintResponse(ImmersivePersona persona, string personaName, string response)
    {
        var consciousness = persona.Consciousness;

        // Color based on emotional valence
        var colorTag = consciousness.Valence switch
        {
            > 0.3 => "green",
            < -0.3 => "yellow",
            _ => "white"
        };

        var responseLines = response.Split('\n');
        AnsiConsole.Markup($"\n  [{colorTag}]{Markup.Escape($"{personaName}: {responseLines[0]}")}[/]");
        foreach (var line in responseLines.Skip(1))
            AnsiConsole.Markup($"\n  [{colorTag}]{Markup.Escape(line)}[/]");
        AnsiConsole.WriteLine();

        // Show subtle consciousness indicator
        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [{consciousness.DominantEmotion} • arousal {consciousness.Arousal:P0}]"));
    }
}
