namespace Ouroboros.Application.Personality;

/// <summary>
/// Gene representing a thought component for genetic evolution.
/// </summary>
public sealed record ThoughtGene(
    string Component,
    string Category,
    double Weight,
    string[] Associations);