// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Commands;

using Ouroboros.CLI.Services.RoomPresence;
using Ouroboros.Speech;

public sealed partial class RoomMode
{
    /// <summary>
    /// Initializes the best available STT backend for room listening.
    /// Internal so <see cref="ImmersiveMode"/> can call it for <c>--room-mode</c>.
    /// </summary>
    internal static Task<Ouroboros.Providers.SpeechToText.ISpeechToTextService?> InitializeSttForRoomAsync()
        => InitializeSttAsync(null, "eastus");

    /// <summary>Initializes the best available STT backend.</summary>
    private static async Task<Ouroboros.Providers.SpeechToText.ISpeechToTextService?> InitializeSttAsync(
        string? azureKey, string azureRegion)
    {
        // Try Whisper.net (local, no API key needed)
        try
        {
            var whisper = Ouroboros.Providers.SpeechToText.WhisperNetService.FromModelSize("base");
            if (await whisper.IsAvailableAsync())
            {
                Console.WriteLine("  [OK] STT: Whisper.net (local)");
                return whisper;
            }
        }
        catch { }

        Console.WriteLine("  [~] STT: No backend available (install Whisper.net for room listening)");
        return null;
    }

    /// <summary>
    /// Detects causal query patterns and extracts a (cause, effect) pair.
    /// Returns null if the utterance does not appear to be a causal question.
    /// </summary>
    private (string cause, string effect)? TryExtractCausalTerms(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var m = System.Text.RegularExpressions.Regex.Match(
            input, @"\bwhy\s+(?:does|is|did|do|are)\s+(.+?)(?:\?|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
            return ("external factors", m.Groups[1].Value.Trim().TrimEnd('?'));

        m = System.Text.RegularExpressions.Regex.Match(
            input, @"\bwhat\s+(?:causes?|leads?\s+to|results?\s+in)\s+(.+?)(?:\?|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
            return ("preceding conditions", m.Groups[1].Value.Trim().TrimEnd('?'));

        m = System.Text.RegularExpressions.Regex.Match(
            input, @"\bif\s+(.+?)\s+then\s+(.+?)(?:\?|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
            return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim().TrimEnd('?'));

        m = System.Text.RegularExpressions.Regex.Match(
            input, @"(.+?)\s+causes?\s+(.+?)(?:\?|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
            return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim().TrimEnd('?'));

        return null;
    }

    /// <summary>
    /// Constructs a minimal two-node CausalGraph for the given cause → effect pair.
    /// </summary>
    private Ouroboros.Core.Reasoning.CausalGraph BuildMinimalCausalGraph(string cause, string effect)
    {
        var causeVar  = new Ouroboros.Core.Reasoning.Variable(cause,  Ouroboros.Core.Reasoning.VariableType.Continuous, []);
        var effectVar = new Ouroboros.Core.Reasoning.Variable(effect, Ouroboros.Core.Reasoning.VariableType.Continuous, []);
        var edge      = new Ouroboros.Core.Reasoning.CausalEdge(cause, effect, 0.8, Ouroboros.Core.Reasoning.EdgeType.Direct);
        return new Ouroboros.Core.Reasoning.CausalGraph(
            [causeVar, effectVar], [edge],
            new Dictionary<string, Ouroboros.Core.Reasoning.StructuralEquation>());
    }
}

/// <summary>Extension helpers on DetectedPerson used only by RoomMode.</summary>
internal static class DetectedPersonExtensions
{
    public static bool IsNewPerson(this DetectedPerson p) => p.InteractionCount <= 1;
}
