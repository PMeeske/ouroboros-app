// <copyright file="PersonalityModels.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

/// <summary>
/// A personality trait with intensity and expression patterns.
/// </summary>
public sealed record PersonalityTrait(
    string Name,
    double Intensity,        // 0.0-1.0 how strongly expressed
    string[] ExpressionPatterns,  // How this trait manifests in speech
    string[] TriggerTopics,      // Topics that activate this trait
    double EvolutionRate)        // How fast this trait adapts
{
    /// <summary>Creates a default trait.</summary>
    public static PersonalityTrait Default(string name) =>
        new(name, 0.5, Array.Empty<string>(), Array.Empty<string>(), 0.1);
}