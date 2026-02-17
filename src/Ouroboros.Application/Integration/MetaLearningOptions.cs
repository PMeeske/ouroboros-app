namespace Ouroboros.Application.Integration;

/// <summary>Options for meta-learning configuration.</summary>
public sealed record MetaLearningOptions(
    string Algorithm = "MAML",
    int InnerSteps = 5,
    double MetaLearningRate = 0.001);