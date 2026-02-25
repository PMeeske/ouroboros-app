namespace Ouroboros.Application.Personality.Consciousness;

/// <summary>
/// Configuration for cognitive processor.
/// </summary>
public sealed record CognitiveProcessorConfig(
    double BroadcastThreshold,
    double ConsciousExperienceLifetimeMinutes)
{
    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    public static CognitiveProcessorConfig Default() => new(
        BroadcastThreshold: 0.5,        // Only broadcast moderately salient experiences
        ConsciousExperienceLifetimeMinutes: 5.0);  // Conscious experiences decay after 5 minutes
}