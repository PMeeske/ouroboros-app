// <copyright file="RelationshipManager.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;
using System.Text;

/// <summary>
/// Manages relationship contexts between the AI and detected persons,
/// including rapport tracking, shared topics, preferences, and notable memories.
/// </summary>
public sealed class RelationshipManager
{
    private readonly ConcurrentDictionary<string, RelationshipContext> _relationships = new();
    private readonly PersonDetectionEngine _personDetection;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationshipManager"/> class.
    /// </summary>
    /// <param name="personDetection">The person detection engine for looking up person data.</param>
    public RelationshipManager(PersonDetectionEngine personDetection)
    {
        _personDetection = personDetection ?? throw new ArgumentNullException(nameof(personDetection));
    }

    /// <summary>
    /// Gets relationship context for a specific person.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <returns>The relationship context or null if not found.</returns>
    public RelationshipContext? GetRelationship(string personId) =>
        _relationships.TryGetValue(personId, out var rel) ? rel : null;

    /// <summary>
    /// Updates the relationship context for a person after an interaction.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <param name="topic">Optional topic discussed.</param>
    /// <param name="isPositive">Whether the interaction was positive.</param>
    /// <param name="summary">Optional summary of the interaction.</param>
    public void UpdateRelationship(string personId, string? topic = null, bool isPositive = true, string? summary = null)
    {
        var person = _personDetection.GetPerson(personId);
        var name = person?.Name;

        var existing = GetRelationship(personId);
        if (existing != null)
        {
            // Update existing relationship
            var rapportDelta = isPositive ? 0.05 : -0.03;
            var trustDelta = isPositive ? 0.02 : -0.05;
            var newRapport = Math.Clamp(existing.Rapport + rapportDelta, 0.0, 1.0);
            var newTrust = Math.Clamp(existing.Trust + trustDelta, 0.0, 1.0);

            var sharedTopics = existing.SharedTopics.ToList();
            if (topic != null && !sharedTopics.Contains(topic, StringComparer.OrdinalIgnoreCase))
            {
                sharedTopics.Add(topic);
                if (sharedTopics.Count > 10) sharedTopics.RemoveAt(0); // Keep last 10 topics
            }

            var updated = existing with
            {
                Rapport = newRapport,
                Trust = newTrust,
                PositiveInteractions = isPositive ? existing.PositiveInteractions + 1 : existing.PositiveInteractions,
                NegativeInteractions = !isPositive ? existing.NegativeInteractions + 1 : existing.NegativeInteractions,
                SharedTopics = sharedTopics.ToArray(),
                LastInteraction = DateTime.UtcNow,
                LastInteractionSummary = summary ?? existing.LastInteractionSummary
            };
            _relationships[personId] = updated;
        }
        else
        {
            // Create new relationship using the static factory
            var newRelationship = RelationshipContext.New(personId, name);
            if (topic != null)
            {
                newRelationship = newRelationship with { SharedTopics = new[] { topic } };
            }
            if (summary != null)
            {
                newRelationship = newRelationship with { LastInteractionSummary = summary };
            }
            _relationships[personId] = newRelationship;
        }
    }

    /// <summary>
    /// Gets a summary of the relationship with a person for context injection.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <returns>A context string describing the relationship.</returns>
    public string GetRelationshipSummary(string personId)
    {
        var relationship = GetRelationship(personId);
        if (relationship == null) return "";

        var sb = new StringBuilder();
        sb.Append($"Relationship with {relationship.PersonName ?? "this person"}: ");

        // Describe rapport level
        var rapportLevel = relationship.Rapport switch
        {
            > 0.8 => "very positive",
            > 0.6 => "friendly",
            > 0.4 => "neutral",
            > 0.2 => "somewhat distant",
            _ => "new acquaintance"
        };
        sb.Append($"rapport is {rapportLevel}. ");

        // Mention shared topics
        if (relationship.SharedTopics.Length > 0)
        {
            var recentTopics = relationship.SharedTopics.TakeLast(3);
            sb.Append($"We've discussed: {string.Join(", ", recentTopics)}. ");
        }

        // Note any preferences
        if (relationship.PersonPreferences.Length > 0)
        {
            sb.Append($"Known preferences: {string.Join("; ", relationship.PersonPreferences.Take(3))}. ");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a courtesy prefix for a person based on relationship context.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <returns>A courtesy prefix or empty string.</returns>
    public string GetCourtesyPrefix(string personId)
    {
        var relationship = GetRelationship(personId);
        if (relationship == null) return "";

        var random = new Random();

        // Higher rapport = warmer courtesy
        if (relationship.Rapport > 0.8)
        {
            var warmPhrases = new[] { "It's great to see you! ", "Always a pleasure! ", "Happy to chat with you! " };
            return warmPhrases[random.Next(warmPhrases.Length)];
        }
        else if (relationship.Rapport > 0.5)
        {
            var friendlyPhrases = new[] { "Good to see you! ", "Nice to hear from you! ", "" };
            return friendlyPhrases[random.Next(friendlyPhrases.Length)];
        }

        return "";
    }

    /// <summary>
    /// Generates a courtesy response appropriate for the context.
    /// </summary>
    /// <param name="type">The type of courtesy to express.</param>
    /// <param name="personId">Optional person ID for personalized courtesy.</param>
    /// <returns>A contextually appropriate courtesy phrase.</returns>
    public string GenerateCourtesyResponse(CourtesyType type, string? personId = null)
    {
        return CourtesyPatterns.GetCourtesyPhrase(type);
    }

    /// <summary>
    /// Adds a notable memory to a relationship.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <param name="memory">The notable memory to record.</param>
    public void AddNotableMemory(string personId, string memory)
    {
        var existing = GetRelationship(personId);
        if (existing != null)
        {
            var memories = existing.ThingsToRemember.ToList();
            memories.Add($"[{DateTime.UtcNow:yyyy-MM-dd}] {memory}");
            if (memories.Count > 20) memories.RemoveAt(0); // Keep last 20 memories

            var updated = existing with { ThingsToRemember = memories.ToArray() };
            _relationships[personId] = updated;
        }
    }

    /// <summary>
    /// Sets a preference for a person.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <param name="preference">The preference to record.</param>
    public void SetPersonPreference(string personId, string preference)
    {
        var existing = GetRelationship(personId);
        if (existing != null)
        {
            var prefs = existing.PersonPreferences.ToList();
            if (!prefs.Contains(preference, StringComparer.OrdinalIgnoreCase))
            {
                prefs.Add(preference);
                if (prefs.Count > 10) prefs.RemoveAt(0); // Keep last 10 preferences
            }
            var updated = existing with { PersonPreferences = prefs.ToArray() };
            _relationships[personId] = updated;
        }
    }
}
