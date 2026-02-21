// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Services.RoomPresence;

using System.Text.RegularExpressions;
using Ouroboros.Application.Personality;
using Ouroboros.Core.Ethics;

/// <summary>
/// Identifies speakers from room utterances by delegating to
/// <see cref="PersonalityEngine"/> for style fingerprinting and Qdrant-backed storage.
///
/// Every store operation is gated through <see cref="IEthicsFramework"/> to ensure
/// that recording a person's conversation is ethically cleared before persistence.
/// </summary>
public sealed class PersonIdentifier
{
    private static readonly Regex NameIntroRegex = new(
        @"\b(?:i'?m|my name is|call me|i am)\s+([A-Z][a-z]{1,30})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly PersonalityEngine _personality;
    private readonly IEthicsFramework _ethics;
    private readonly string _agentId;

    // Rolling buffer of recent utterance texts for style matching
    private readonly Queue<string> _recentBuffer = new(capacity: 20);

    public PersonIdentifier(PersonalityEngine personality, IEthicsFramework ethics, string agentId = "Iaret")
    {
        _personality = personality;
        _ethics = ethics;
        _agentId = agentId;
    }

    /// <summary>
    /// Attempts to identify the speaker of <paramref name="utterance"/>.
    /// Returns the detected person profile (may be Unknown with no Name).
    /// </summary>
    public async Task<DetectedPerson> IdentifyAsync(RoomUtterance utterance, CancellationToken ct = default)
    {
        _recentBuffer.Enqueue(utterance.Text);
        if (_recentBuffer.Count > 20) _recentBuffer.Dequeue();

        var result = await _personality.DetectPersonAsync(
            utterance.Text,
            [.. _recentBuffer],
            ct).ConfigureAwait(false);

        return result.Person;
    }

    /// <summary>
    /// Records the utterance to Qdrant (via PersonalityEngine) after an ethics check.
    /// If the ethics framework denies storage, the call is a no-op.
    /// </summary>
    public async Task RecordUtteranceAsync(
        DetectedPerson person, string utteranceText, CancellationToken ct = default)
    {
        var clearance = await _ethics.EvaluateActionAsync(
            new ProposedAction
            {
                ActionType = "store_conversation",
                Description = $"Record ambient utterance from {person.Name ?? "unknown speaker"} to memory",
                Parameters = new Dictionary<string, object>
                {
                    ["personId"] = person.Id,
                    ["personName"] = person.Name ?? "unknown",
                    ["textLength"] = utteranceText.Length,
                },
                PotentialEffects = ["Persist person's speech to Qdrant long-term memory"],
            },
            new ActionContext
            {
                AgentId = _agentId,
                Environment = "room_presence",
                State = new Dictionary<string, object>
                {
                    ["mode"] = "ambient_listening",
                    ["isNameKnown"] = person.Name != null,
                },
            },
            ct).ConfigureAwait(false);

        if (!clearance.IsSuccess || !clearance.Value.IsPermitted)
            return;

        await _personality.StoreConversationMemoryAsync(
            personaName: _agentId,
            userMessage: utteranceText,
            assistantResponse: string.Empty, // ambient — no response yet
            topic: null,
            detectedMood: null,
            significance: 0.4,
            ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves prior conversation context for a known person on a given topic.
    /// Returns an empty string when no matching memories exist.
    /// </summary>
    public async Task<string> GetPersonContextAsync(
        DetectedPerson person, string topic, CancellationToken ct = default)
    {
        var memories = await _personality.RecallConversationsAsync(
            query: topic,
            personaName: person.Name ?? person.Id,
            limit: 5,
            minScore: 0.5f,
            ct: ct).ConfigureAwait(false);

        if (memories.Count == 0)
            return string.Empty;

        return string.Join("\n", memories.Select(m => $"[{m.Timestamp:g}] {m.UserMessage}"));
    }

    /// <summary>
    /// Extracts an explicit name from an introduction phrase, e.g. "I'm Alice".
    /// Returns null if no name is found.
    /// </summary>
    public static string? ExtractIntroductionName(string text)
    {
        var match = NameIntroRegex.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }
}
