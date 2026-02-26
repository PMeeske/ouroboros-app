// <copyright file="ConsciousnessTypes.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

/// <summary>
/// A stimulus that can trigger conditioned responses.
/// </summary>
public sealed record Stimulus(
    string Id,
    string Pattern,              // The pattern/content that constitutes this stimulus
    StimulusType Type,
    double Salience,             // 0-1: how attention-grabbing this stimulus is
    string[] Keywords,           // Keywords that activate this stimulus
    string? Category,            // Category for grouping related stimuli
    DateTime FirstEncounter,
    DateTime LastEncounter,
    int EncounterCount)
{
    /// <summary>Creates a new neutral stimulus.</summary>
    public static Stimulus CreateNeutral(string pattern, string[] keywords, string? category = null) => new(
        Id: Guid.NewGuid().ToString(),
        Pattern: pattern,
        Type: StimulusType.Neutral,
        Salience: 0.5,
        Keywords: keywords,
        Category: category,
        FirstEncounter: DateTime.UtcNow,
        LastEncounter: DateTime.UtcNow,
        EncounterCount: 1);

    /// <summary>Creates an unconditioned stimulus with high salience.</summary>
    public static Stimulus CreateUnconditioned(string pattern, string[] keywords, string? category = null) => new(
        Id: Guid.NewGuid().ToString(),
        Pattern: pattern,
        Type: StimulusType.Unconditioned,
        Salience: 0.9,
        Keywords: keywords,
        Category: category,
        FirstEncounter: DateTime.UtcNow,
        LastEncounter: DateTime.UtcNow,
        EncounterCount: 1);

    /// <summary>Checks if input matches this stimulus.</summary>
    public bool Matches(string input)
    {
        string lower = input.ToLowerInvariant();
        return Keywords.Any(k => lower.Contains(k.ToLower())) ||
               lower.Contains(Pattern.ToLower());
    }
}