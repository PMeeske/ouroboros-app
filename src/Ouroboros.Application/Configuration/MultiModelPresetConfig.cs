namespace Ouroboros.Application.Configuration;

/// <summary>
/// Configuration for a single model slot within a multi-model orchestrator preset.
/// </summary>
public record ModelSlotConfig
{
    /// <summary>Role key used by the orchestrator (e.g. "general", "coder", "reasoner").</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Model name / identifier (e.g. "claude-sonnet-4-20250514", "deepseek-coder:33b").</summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>Provider endpoint type (e.g. "anthropic", "ollama", "openai").</summary>
    public string ProviderType { get; init; } = "ollama";

    /// <summary>Endpoint URL. Null means use default for the provider type.</summary>
    public string? Endpoint { get; init; }

    /// <summary>API key. Null means resolve from environment variable.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Environment variable name to resolve the API key from when <see cref="ApiKey"/> is null.</summary>
    public string? ApiKeyEnvVar { get; init; }

    /// <summary>Sampling temperature override. Null means use preset default.</summary>
    public double? Temperature { get; init; }

    /// <summary>Max tokens override. Null means use preset default.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>Tags for orchestrator model selection heuristics.</summary>
    public string[] Tags { get; init; } = [];

    /// <summary>Expected average latency in milliseconds (used for orchestrator scheduling).</summary>
    public int AvgLatencyMs { get; init; } = 1000;
}

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
