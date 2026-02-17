namespace Ouroboros.Application.Personality;

/// <summary>
/// Configuration for the inner dialog engine.
/// </summary>
public sealed record InnerDialogConfig(
    bool EnableEmotionalProcessing = true,
    bool EnableMemoryRecall = true,
    bool EnableEthicalChecks = true,
    bool EnableCreativeThinking = true,
    bool EnableAutonomousThoughts = false,
    int MaxThoughts = 10,
    double MinConfidenceThreshold = 0.3,
    TimeSpan MaxProcessingTime = default,
    InnerThoughtType[]? EnabledThoughtTypes = null,
    string[]? EnabledProviders = null,
    double AutonomousThoughtProbability = 0.3,
    ThoughtPriority MinPriority = ThoughtPriority.Background,
    string? TopicHint = null,
    double ProcessingIntensity = 0.7,
    bool IncludeEmotional = true,
    bool IncludeEthical = true,
    bool IncludeCreative = true)
{
    /// <summary>Default configuration.</summary>
    public static InnerDialogConfig Default => new()
    {
        MaxProcessingTime = TimeSpan.FromSeconds(5),
        EnabledThoughtTypes = Array.Empty<InnerThoughtType>(), // Empty = all enabled
        EnabledProviders = Array.Empty<string>() // Empty = all enabled
    };

    /// <summary>Fast configuration for quick responses.</summary>
    public static InnerDialogConfig Fast => new(
        EnableEmotionalProcessing: true,
        EnableMemoryRecall: false,
        EnableEthicalChecks: false,
        EnableCreativeThinking: false,
        EnableAutonomousThoughts: false,
        MaxThoughts: 5,
        MinConfidenceThreshold: 0.4,
        MaxProcessingTime: TimeSpan.FromSeconds(2),
        EnabledThoughtTypes: new[] { InnerThoughtType.Observation, InnerThoughtType.Analytical, InnerThoughtType.Decision },
        EnabledProviders: Array.Empty<string>(),
        AutonomousThoughtProbability: 0,
        MinPriority: ThoughtPriority.Normal);

    /// <summary>Deep configuration for thorough analysis.</summary>
    public static InnerDialogConfig Deep => new(
        EnableEmotionalProcessing: true,
        EnableMemoryRecall: true,
        EnableEthicalChecks: true,
        EnableCreativeThinking: true,
        EnableAutonomousThoughts: true,
        MaxThoughts: 20,
        MinConfidenceThreshold: 0.2,
        MaxProcessingTime: TimeSpan.FromSeconds(10),
        EnabledThoughtTypes: Array.Empty<InnerThoughtType>(),
        EnabledProviders: Array.Empty<string>(),
        AutonomousThoughtProbability: 0.5,
        MinPriority: ThoughtPriority.Background);

    /// <summary>Autonomous thinking configuration (no input required).</summary>
    public static InnerDialogConfig Autonomous => new(
        EnableEmotionalProcessing: true,
        EnableMemoryRecall: true,
        EnableEthicalChecks: false,
        EnableCreativeThinking: true,
        EnableAutonomousThoughts: true,
        MaxThoughts: 15,
        MinConfidenceThreshold: 0.2,
        MaxProcessingTime: TimeSpan.FromSeconds(30),
        EnabledThoughtTypes: new[]
        {
            InnerThoughtType.Curiosity, InnerThoughtType.Wandering, InnerThoughtType.Metacognitive,
            InnerThoughtType.Musing, InnerThoughtType.Consolidation, InnerThoughtType.Playful
        },
        EnabledProviders: Array.Empty<string>(),
        AutonomousThoughtProbability: 1.0,
        MinPriority: ThoughtPriority.Background);

    /// <summary>Checks if a thought type is enabled.</summary>
    public bool IsThoughtTypeEnabled(InnerThoughtType type) =>
        EnabledThoughtTypes == null || EnabledThoughtTypes.Length == 0 || EnabledThoughtTypes.Contains(type);

    /// <summary>Checks if a provider is enabled.</summary>
    public bool IsProviderEnabled(string providerName) =>
        EnabledProviders == null || EnabledProviders.Length == 0 || EnabledProviders.Contains(providerName);
}