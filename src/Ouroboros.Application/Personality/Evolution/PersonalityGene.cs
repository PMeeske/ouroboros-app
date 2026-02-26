// <copyright file="PersonalityEvolution.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

/// <summary>
/// Gene type for personality chromosome - represents a single aspect of personality.
/// </summary>
public sealed record PersonalityGene(string Key, double Value);