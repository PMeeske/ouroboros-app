// <copyright file="EpisodicMemoryEntry.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

/// <summary>
/// Represents an episodic memory entry with experiential metadata.
/// </summary>
public class EpisodicMemoryEntry
{
    public Guid Id { get; set; }
    public string Content { get; set; } = "";
    public string Emotion { get; set; } = "neutral";
    public double Significance { get; set; }
    public DateTime Timestamp { get; set; }
    public int RecallCount { get; set; }
    public DateTime? LastRecalled { get; set; }
}
