namespace Ouroboros.Application.Configuration;

/// <summary>
/// A complete multi-model orchestrator preset that defines a master model
/// and one or more specialized sub-models.
/// </summary>
public record MultiModelPresetConfig
{
    /// <summary>Unique name of the preset (e.g. "anthropic-ollama").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Human-readable description of the preset.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The role key of the master / default model (must match one of the <see cref="Models"/> entries).</summary>
    public string MasterRole { get; init; } = "general";

    /// <summary>Default sampling temperature for all models (individual slots can override).</summary>
    public double DefaultTemperature { get; init; } = 0.7;

    /// <summary>Default max tokens for all models (individual slots can override).</summary>
    public int DefaultMaxTokens { get; init; } = 2048;

    /// <summary>Default request timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>Whether to enable performance metric tracking in the orchestrator.</summary>
    public bool EnableMetrics { get; init; } = true;

    /// <summary>The model slots that make up this preset.</summary>
    public ModelSlotConfig[] Models { get; init; } = [];
}
