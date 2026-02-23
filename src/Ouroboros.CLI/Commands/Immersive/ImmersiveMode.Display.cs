// <copyright file="ImmersiveMode.Display.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Options;

public sealed partial class ImmersiveMode
{
    private void PrintImmersiveBanner(string personaName)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
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
");
        Console.ResetColor();

        Console.WriteLine($"  Awakening: {personaName}");
        Console.WriteLine("  ---------------------------------------------------------------------------");
        Console.WriteLine("  CONSCIOUSNESS:    who are you | describe yourself | introspect");
        Console.WriteLine("  STATE:            my state | system status | what do you know");
        Console.WriteLine("  SKILLS:           list skills | run <skill> | learn about <topic>");
        Console.WriteLine("  TOOLS:            add tool <name> | smart tool for <goal> | tool stats");
        Console.WriteLine("  PIPELINE:         tokens | emergence <topic> | <step1> | <step2>");
        Console.WriteLine("  LEARNING:         connections | tool stats | google search <query>");
        Console.WriteLine("  INDEX:            reindex | reindex incremental | index search <query> | index stats");
        Console.WriteLine("  MEMORY:           remember <topic> | memory stats | save yourself | snapshot");
        Console.WriteLine("  MIND:             mind state | think about <topic> | start mind | stop mind | interests");
        Console.WriteLine("  EXIT:             goodbye | exit | quit");
        Console.WriteLine("  ---------------------------------------------------------------------------");
        Console.WriteLine();
    }

    private void PrintConsciousnessState(ImmersivePersona persona)
    {
        var consciousness = persona.Consciousness;
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"\n  +-- Consciousness State ---------------------------------------------+");
        Console.WriteLine($"  | Emotion: {consciousness.DominantEmotion,-15} Valence: {consciousness.Valence:+0.00;-0.00}        |");
        Console.WriteLine($"  | Arousal: {consciousness.Arousal:P0,-15} Attention: {consciousness.CurrentFocus,-15} |");
        Console.WriteLine($"  | Mode: {consciousness.CurrentFocus,-58} |");
        Console.WriteLine($"  +--------------------------------------------------------------------+");
        Console.ResetColor();
    }

    private void PrintResponse(ImmersivePersona persona, string personaName, string response)
    {
        var consciousness = persona.Consciousness;

        // Color based on emotional valence
        Console.ForegroundColor = consciousness.Valence switch
        {
            > 0.3 => ConsoleColor.Green,
            < -0.3 => ConsoleColor.Yellow,
            _ => ConsoleColor.White
        };

        var responseLines = response.Split('\n');
        Console.Write($"\n  {personaName}: {responseLines[0]}");
        foreach (var line in responseLines.Skip(1))
            Console.Write($"\n  {line}");
        Console.WriteLine();
        Console.ResetColor();

        // Show subtle consciousness indicator
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  [{consciousness.DominantEmotion} • arousal {consciousness.Arousal:P0}]");
        Console.ResetColor();
    }
}
