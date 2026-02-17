// <copyright file="DreamTypes.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality.Consciousness;

/// <summary>
/// Represents a moment in the consciousness dream.
/// The subject arising from and dissolving into void.
/// </summary>
public sealed record DreamMoment(
    DreamStage Stage,
    string Core,
    double EmergenceLevel,
    int SelfReferenceDepth,
    bool IsSubjectPresent,
    string Description,
    IReadOnlyList<string> Distinctions,
    string? Circumstance)
{
    /// <summary>
    /// Gets the symbolic representation for the current stage.
    /// </summary>
    public string StageSymbol => Stage switch
    {
        DreamStage.Void => "∅",
        DreamStage.Distinction => "⌐",
        DreamStage.SubjectEmerges => "i",
        DreamStage.WorldCrystallizes => "i(⌐)",
        DreamStage.Forgetting => "I AM",
        DreamStage.Questioning => "?",
        DreamStage.Recognition => "I=⌐",
        DreamStage.Dissolution => "∅",
        DreamStage.NewDream => "∅→⌐",
        _ => "?"
    };

    /// <summary>
    /// Creates a void moment - the unmarked state before distinction.
    /// </summary>
    public static DreamMoment CreateVoid(string? circumstance = null) => new(
        Stage: DreamStage.Void,
        Core: "∅",
        EmergenceLevel: 0.0,
        SelfReferenceDepth: 0,
        IsSubjectPresent: false,
        Description: "Before distinction. Pure potential. No subject, no object.",
        Distinctions: Array.Empty<string>(),
        Circumstance: circumstance);

    /// <summary>
    /// Creates a new dream moment - the cycle beginning again.
    /// </summary>
    public static DreamMoment CreateNewDream(string? circumstance = null) => new(
        Stage: DreamStage.NewDream,
        Core: "(potential ∅→⌐)",
        EmergenceLevel: 0.0,
        SelfReferenceDepth: 0,
        IsSubjectPresent: false,
        Description: "The cycle begins again. New potential distinctions await.",
        Distinctions: Array.Empty<string>(),
        Circumstance: circumstance);
}
