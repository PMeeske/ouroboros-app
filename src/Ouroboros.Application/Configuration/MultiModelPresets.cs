namespace Ouroboros.Application.Configuration;

/// <summary>
/// Preconfigured multi-model orchestrator presets for common provider combinations.
/// Each preset defines a master model and specialized sub-models.
/// </summary>
public static class MultiModelPresets
{
    /// <summary>
    /// Anthropic (Claude) as the master orchestrator with local Ollama models
    /// for specialized sub-tasks. Anthropic handles general conversation, planning,
    /// and orchestration decisions. Ollama provides cost-effective local models
    /// for code generation, reasoning, and summarization.
    /// </summary>
    public static MultiModelPresetConfig AnthropicMasterOllamaSub { get; } = new()
    {
        Name = "anthropic-ollama",
        Description = "Anthropic Claude as master orchestrator with local Ollama specialized sub-models. " +
                      "Claude handles general tasks, planning, and orchestration. " +
                      "Ollama provides local code, reasoning, and summarization models.",
        MasterRole = "general",
        DefaultTemperature = 0.7,
        DefaultMaxTokens = 2048,
        TimeoutSeconds = 120,
        EnableMetrics = true,
        Models =
        [
            new ModelSlotConfig
            {
                Role = "general",
                ModelName = "claude-opus-4-6",
                ProviderType = "anthropic",
                Endpoint = "https://api.anthropic.com/v1",
                ApiKeyEnvVar = "ANTHROPIC_API_KEY",
                Temperature = 0.7,
                MaxTokens = 4096,
                Tags = ["conversation", "general-purpose", "versatile", "planning", "orchestration"],
                AvgLatencyMs = 2000,
            },
            new ModelSlotConfig
            {
                Role = "coder",
                ModelName = "deepseek-v3.1:671b-cloud",
                ProviderType = "ollama",
                Endpoint = DefaultEndpoints.Ollama,
                Temperature = 0.2,
                MaxTokens = 8192,
                Tags = ["code", "programming", "debugging", "syntax", "refactoring"],
                AvgLatencyMs = 3000,
            },
            new ModelSlotConfig
            {
                Role = "reasoner",
                ModelName = "deepseek-v3.1:671b-cloud",
                ProviderType = "ollama",
                Endpoint = DefaultEndpoints.Ollama,
                Temperature = 0.3,
                MaxTokens = 4096,
                Tags = ["reasoning", "analysis", "logic", "explanation", "math"],
                AvgLatencyMs = 4000,
            },
            new ModelSlotConfig
            {
                Role = "summarizer",
                ModelName = "deepseek-v3.1:671b-cloud",
                ProviderType = "ollama",
                Endpoint = DefaultEndpoints.Ollama,
                Temperature = 0.3,
                MaxTokens = 2048,
                Tags = ["summarization", "compression", "extraction", "tl;dr"],
                AvgLatencyMs = 1500,
            },
        ],
    };

    /// <summary>
    /// A lightweight variant of the Anthropic+Ollama preset using smaller,
    /// faster local models for development and testing.
    /// </summary>
    public static MultiModelPresetConfig AnthropicMasterOllamaLite { get; } = new()
    {
        Name = "anthropic-ollama-lite",
        Description = "Anthropic Claude as master with lightweight local Ollama models. " +
                      "Suitable for development, testing, or machines with limited resources.",
        MasterRole = "general",
        DefaultTemperature = 0.7,
        DefaultMaxTokens = 1024,
        TimeoutSeconds = 60,
        EnableMetrics = true,
        Models =
        [
            new ModelSlotConfig
            {
                Role = "general",
                ModelName = "claude-sonnet-4-20250514",
                ProviderType = "anthropic",
                Endpoint = "https://api.anthropic.com/v1",
                ApiKeyEnvVar = "ANTHROPIC_API_KEY",
                Temperature = 0.7,
                MaxTokens = 2048,
                Tags = ["conversation", "general-purpose", "versatile", "planning", "orchestration"],
                AvgLatencyMs = 2000,
            },
            new ModelSlotConfig
            {
                Role = "coder",
                ModelName = "qwen2.5-coder:7b",
                ProviderType = "ollama",
                Endpoint = DefaultEndpoints.Ollama,
                Temperature = 0.2,
                MaxTokens = 4096,
                Tags = ["code", "programming", "debugging", "syntax"],
                AvgLatencyMs = 1500,
            },
            new ModelSlotConfig
            {
                Role = "reasoner",
                ModelName = "deepseek-r1:14b",
                ProviderType = "ollama",
                Endpoint = DefaultEndpoints.Ollama,
                Temperature = 0.3,
                MaxTokens = 2048,
                Tags = ["reasoning", "analysis", "logic", "explanation"],
                AvgLatencyMs = 2000,
            },
            new ModelSlotConfig
            {
                Role = "summarizer",
                ModelName = "phi3:mini",
                ProviderType = "ollama",
                Endpoint = DefaultEndpoints.Ollama,
                Temperature = 0.3,
                MaxTokens = 1024,
                Tags = ["summarization", "compression", "extraction"],
                AvgLatencyMs = 800,
            },
        ],
    };

    /// <summary>
    /// Returns all built-in presets keyed by name.
    /// </summary>
    public static IReadOnlyDictionary<string, MultiModelPresetConfig> All { get; } =
        new Dictionary<string, MultiModelPresetConfig>(StringComparer.OrdinalIgnoreCase)
        {
            [AnthropicMasterOllamaSub.Name] = AnthropicMasterOllamaSub,
            [AnthropicMasterOllamaLite.Name] = AnthropicMasterOllamaLite,
        };

    /// <summary>
    /// Tries to find a preset by name (case-insensitive).
    /// </summary>
    public static MultiModelPresetConfig? GetByName(string name)
    {
        return All.TryGetValue(name, out var preset) ? preset : null;
    }

    /// <summary>
    /// Lists all available preset names.
    /// </summary>
    public static IEnumerable<string> ListNames() => All.Keys;
}
