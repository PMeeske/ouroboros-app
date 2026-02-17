namespace Ouroboros.Application.Personality;

/// <summary>
/// Relationship context with a specific person.
/// </summary>
public sealed record RelationshipContext(
    string PersonId,
    string? PersonName,
    double Rapport,                     // 0-1: how well the relationship is going
    double Trust,                       // 0-1: trust level
    int PositiveInteractions,
    int NegativeInteractions,
    string[] SharedTopics,              // Topics discussed together
    string[] PersonPreferences,         // Known preferences of this person
    string[] ThingsToRemember,          // Important things to remember about them
    DateTime FirstInteraction,
    DateTime LastInteraction,
    string LastInteractionSummary)
{
    /// <summary>Creates a new relationship context.</summary>
    public static RelationshipContext New(string personId, string? name) => new(
        PersonId: personId,
        PersonName: name,
        Rapport: 0.5,
        Trust: 0.5,
        PositiveInteractions: 0,
        NegativeInteractions: 0,
        SharedTopics: Array.Empty<string>(),
        PersonPreferences: Array.Empty<string>(),
        ThingsToRemember: Array.Empty<string>(),
        FirstInteraction: DateTime.UtcNow,
        LastInteraction: DateTime.UtcNow,
        LastInteractionSummary: "");
}