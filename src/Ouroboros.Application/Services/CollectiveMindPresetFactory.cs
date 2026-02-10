using Ouroboros.Application.Configuration;

namespace Ouroboros.Application.Services;

/// <summary>
/// Factory that creates <c>CollectiveMind</c> instances from multi-model presets.
/// Adds each preset model slot as a pathway with the correct provider type,
/// and designates the master role as the CollectiveMind master.
/// </summary>
public static class CollectiveMindPresetFactory
{
    /// <summary>
    /// Creates a <c>CollectiveMind</c> from a <see cref="MultiModelPresetConfig"/>.
    /// Each model slot becomes a pathway; the master role is set as the collective master.
    /// </summary>
    /// <param name="preset">The multi-model preset configuration.</param>
    /// <param name="settings">Runtime settings for chat models.</param>
    /// <returns>A configured <c>CollectiveMind</c> instance.</returns>
    public static CollectiveMind CreateFromPreset(MultiModelPresetConfig preset, ChatRuntimeSettings settings)
    {
        var mind = new CollectiveMind();

        foreach (var slot in preset.Models)
        {
            string providerType = (slot.ProviderType ?? "ollama").ToLowerInvariant();
            string endpoint = slot.Endpoint ?? GetDefaultEndpoint(providerType);

            // Resolve API key from explicit value or environment variable
            string? apiKey = slot.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(slot.ApiKeyEnvVar))
            {
                apiKey = Environment.GetEnvironmentVariable(slot.ApiKeyEnvVar);
            }

            // Parse provider type to ChatEndpointType
            if (!Enum.TryParse<ChatEndpointType>(providerType, ignoreCase: true, out var endpointType))
            {
                endpointType = providerType.ToLowerInvariant() switch
                {
                    "ollama" => ChatEndpointType.OllamaLocal,
                    _ => ChatEndpointType.OpenAiCompatible,
                };
            }

            // Build per-slot settings with temperature/maxTokens overrides
            double temperature = slot.Temperature ?? preset.DefaultTemperature;
            int maxTokens = slot.MaxTokens ?? preset.DefaultMaxTokens;
            var slotSettings = new ChatRuntimeSettings(temperature, maxTokens, preset.TimeoutSeconds, false, settings.Culture);

            // Use role as the pathway name (e.g. "general", "coder", "reasoner", "summarizer")
            string pathwayName = slot.Role;

            try
            {
                mind.AddPathway(pathwayName, endpointType, slot.ModelName, endpoint, apiKey, slotSettings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [preset] Could not add pathway '{pathwayName}' ({slot.ProviderType}:{slot.ModelName}): {ex.Message}");
            }
        }

        // Set the master role
        if (!string.IsNullOrWhiteSpace(preset.MasterRole))
        {
            mind.SetMaster(preset.MasterRole);
        }
        else if (mind.Pathways.Count > 0)
        {
            mind.SetFirstAsMaster();
        }

        return mind;
    }

    private static string GetDefaultEndpoint(string providerType)
    {
        return providerType.ToLowerInvariant() switch
        {
            "anthropic" => "https://api.anthropic.com/v1",
            "openai" => "https://api.openai.com/v1",
            "groq" => "https://api.groq.com/openai/v1",
            "together" => "https://api.together.xyz/v1",
            "fireworks" => "https://api.fireworks.ai/inference/v1",
            "deepseek" => "https://api.deepseek.com/v1",
            "mistral" => "https://api.mistral.ai/v1",
            "ollama" => "http://localhost:11434",
            _ => "http://localhost:11434",
        };
    }
}
