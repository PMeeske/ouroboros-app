namespace Ouroboros.Application.Integration;

/// <summary>Options for adapter learning configuration.</summary>
public sealed record AdapterLearningOptions(
    int Rank = 8,
    double LearningRate = 0.0001,
    int BatchSize = 32);