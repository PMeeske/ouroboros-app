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
    private void PrintImmersiveBanner(string personaName)
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

    private void PrintConsciousnessState(ImmersivePersona persona)
    {
        var consciousness = persona.Consciousness;
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Consciousness State"));
        AnsiConsole.MarkupLine($"[rgb(148,103,189)]  {Markup.Escape($"Emotion: {consciousness.DominantEmotion,-15} Valence: {consciousness.Valence:+0.00;-0.00}")}[/]");
        AnsiConsole.MarkupLine($"[rgb(148,103,189)]  {Markup.Escape($"Arousal: {consciousness.Arousal:P0,-15} Attention: {consciousness.CurrentFocus,-15}")}[/]");
        AnsiConsole.MarkupLine($"[rgb(148,103,189)]  {Markup.Escape($"Mode: {consciousness.CurrentFocus}")}[/]");
        AnsiConsole.Write(OuroborosTheme.ThemedRule());
    }

    private void PrintResponse(ImmersivePersona persona, string personaName, string response)
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
