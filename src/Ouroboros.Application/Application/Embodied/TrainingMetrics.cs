namespace Ouroboros.Application.Embodied;

/// <summary>
/// Training metrics from a batch update.
/// </summary>
/// <param name="PolicyLoss">Policy gradient loss</param>
/// <param name="ValueLoss">Value function loss</param>
/// <param name="Entropy">Policy entropy (exploration measure)</param>
/// <param name="AverageReward">Average reward in batch</param>
/// <param name="BatchSize">Number of transitions in batch</param>
public sealed record TrainingMetrics(
    double PolicyLoss,
    double ValueLoss,
    double Entropy,
    double AverageReward,
    int BatchSize);