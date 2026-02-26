// <copyright file="InnerDialogTypes.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

/// <summary>
/// A single thought in the inner dialog sequence.
/// </summary>
public sealed record InnerThought(
    Guid Id,
    InnerThoughtType Type,
    string Content,
    double Confidence,           // 0-1: how confident this thought is
    double Relevance,            // 0-1: how relevant to the input
    string? TriggeringTrait,     // Which personality trait triggered this
    DateTime Timestamp,
    ThoughtOrigin Origin = ThoughtOrigin.Reactive,
    ThoughtPriority Priority = ThoughtPriority.Normal,
    Guid? ParentThoughtId = null,        // For chained thoughts
    string[]? Tags = null,               // Flexible tagging
    Dictionary<string, object>? Metadata = null)  // Extensible metadata
{
    /// <summary>Creates a new reactive thought (triggered by input).</summary>
    public static InnerThought Create(InnerThoughtType type, string content, double confidence = 0.7, string? trait = null) =>
        new(Guid.NewGuid(), type, content, confidence, 0.8, trait, DateTime.UtcNow);

    /// <summary>Creates an autonomous thought (self-initiated).</summary>
    public static InnerThought CreateAutonomous(
        InnerThoughtType type,
        string content,
        double confidence = 0.6,
        ThoughtPriority priority = ThoughtPriority.Background,
        string[]? tags = null) =>
        new(Guid.NewGuid(), type, content, confidence, 0.5, null, DateTime.UtcNow,
            Origin: ThoughtOrigin.Autonomous, Priority: priority, Tags: tags);

    /// <summary>Creates a chained thought (derived from another thought).</summary>
    public static InnerThought CreateChained(
        Guid parentId,
        InnerThoughtType type,
        string content,
        double confidence = 0.7) =>
        new(Guid.NewGuid(), type, content, confidence, 0.7, null, DateTime.UtcNow,
            Origin: ThoughtOrigin.Chained, ParentThoughtId: parentId);

    /// <summary>Whether this is an autonomous (self-initiated) thought.</summary>
    public bool IsAutonomous => Origin == ThoughtOrigin.Autonomous;

    /// <summary>Whether this thought has children in a chain.</summary>
    public bool IsChainParent => ParentThoughtId == null && Origin != ThoughtOrigin.Chained;
}