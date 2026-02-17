namespace Ouroboros.Application.Integration;

/// <summary>Feature flags configuration.</summary>
public sealed class FeaturesConfig
{
    /// <summary>Gets or sets whether episodic memory is enabled.</summary>
    public bool EnableEpisodicMemory { get; set; } = true;

    /// <summary>Gets or sets whether adapter learning is enabled.</summary>
    public bool EnableAdapterLearning { get; set; } = true;

    /// <summary>Gets or sets whether MeTTa reasoning is enabled.</summary>
    public bool EnableMeTTa { get; set; } = true;

    /// <summary>Gets or sets whether hierarchical planning is enabled.</summary>
    public bool EnablePlanning { get; set; } = true;

    /// <summary>Gets or sets whether reflection is enabled.</summary>
    public bool EnableReflection { get; set; } = true;

    /// <summary>Gets or sets whether program synthesis is enabled.</summary>
    public bool EnableSynthesis { get; set; } = false;

    /// <summary>Gets or sets whether world model is enabled.</summary>
    public bool EnableWorldModel { get; set; } = false;

    /// <summary>Gets or sets whether multi-agent is enabled.</summary>
    public bool EnableMultiAgent { get; set; } = false;

    /// <summary>Gets or sets whether causal reasoning is enabled.</summary>
    public bool EnableCausal { get; set; } = true;

    /// <summary>Gets or sets whether meta-learning is enabled.</summary>
    public bool EnableMetaLearning { get; set; } = false;

    /// <summary>Gets or sets whether embodied simulation is enabled.</summary>
    public bool EnableEmbodied { get; set; } = false;

    /// <summary>Gets or sets whether consciousness is enabled.</summary>
    public bool EnableConsciousness { get; set; } = true;

    /// <summary>Gets or sets whether benchmarks are enabled.</summary>
    public bool EnableBenchmarks { get; set; } = true;

    /// <summary>Gets or sets whether cognitive loop is enabled.</summary>
    public bool EnableCognitiveLoop { get; set; } = false;
}