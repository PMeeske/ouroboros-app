using LangChain.Providers.Ollama;
using Ouroboros.Application.Configuration;
using Ouroboros.Providers;

namespace Ouroboros.Application.Services;

/// <summary>
/// Factory that materializes <see cref="MultiModelPresetConfig"/> presets into
/// live <see cref="IChatCompletionModel"/> instances suitable for the orchestrator.
/// </summary>
public static class MultiModelPresetFactory
{
    /// <summary>
    /// Creates a dictionary of role → <see cref="IChatCompletionModel"/> from a preset configuration.
    /// </summary>
    public static Dictionary<string, IChatCompletionModel> CreateModels(
        MultiModelPresetConfig preset,
        string? culture = null)
    {
        var models = new Dictionary<string, IChatCompletionModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var slot in preset.Models)
        {
            var model = CreateModelFromSlot(slot, preset, culture);
            models[slot.Role] = model;
        }

        return models;
    }

    /// <summary>
    /// Creates a single <see cref="IChatCompletionModel"/> from a <see cref="ModelSlotConfig"/>.
    /// </summary>
    public static IChatCompletionModel CreateModelFromSlot(
        ModelSlotConfig slot,
        MultiModelPresetConfig preset,
        string? culture = null)
    {
        double temperature = slot.Temperature ?? preset.DefaultTemperature;
        int maxTokens = slot.MaxTokens ?? preset.DefaultMaxTokens;
        var settings = new ChatRuntimeSettings(temperature, maxTokens, preset.TimeoutSeconds, false, culture);

        string providerType = (slot.ProviderType ?? "ollama").ToLowerInvariant();

        return providerType switch
        {
            "ollama" => CreateOllamaModel(slot, settings, culture),
            _ => CreateRemoteModel(slot, settings, providerType),
        };
    }

    private static IChatCompletionModel CreateOllamaModel(
        ModelSlotConfig slot,
        ChatRuntimeSettings settings,
        string? culture)
    {
        string endpoint = slot.Endpoint ?? "http://localhost:11434";
        var provider = new OllamaProvider(endpoint);
        var ollamaModel = new OllamaChatModel(provider, slot.ModelName);

        // Apply known Ollama presets based on model name
        ApplyOllamaPresets(ollamaModel, slot.ModelName, slot.Role);

        return new OllamaChatAdapter(ollamaModel, culture);
    }

    private static IChatCompletionModel CreateRemoteModel(
        ModelSlotConfig slot,
        ChatRuntimeSettings settings,
        string providerType)
    {
        string? apiKey = slot.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(slot.ApiKeyEnvVar))
        {
            apiKey = Environment.GetEnvironmentVariable(slot.ApiKeyEnvVar);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"API key required for {providerType} model '{slot.ModelName}' (role: {slot.Role}). " +
                $"Set the {slot.ApiKeyEnvVar ?? "API key"} environment variable or provide it in the preset configuration.");
        }

        string endpoint = slot.Endpoint ?? GetDefaultEndpoint(providerType);

        if (!Enum.TryParse<ChatEndpointType>(providerType, ignoreCase: true, out var endpointType))
        {
            endpointType = ChatEndpointType.OpenAiCompatible;
        }

        return ServiceFactory.CreateRemoteChatModel(endpoint, apiKey, slot.ModelName, settings, endpointType);
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
            _ => "http://localhost:11434",
        };
    }

    private static void ApplyOllamaPresets(OllamaChatModel model, string modelName, string role)
    {
        try
        {
            string n = (modelName ?? string.Empty).ToLowerInvariant();
            if (n.StartsWith("deepseek-coder:33b"))
                model.Settings = OllamaPresets.DeepSeekCoder33B;
            else if (n.StartsWith("llama3"))
                model.Settings = role.Equals("summarizer", StringComparison.OrdinalIgnoreCase)
                    ? OllamaPresets.Llama3Summarize
                    : OllamaPresets.Llama3General;
            else if (n.StartsWith("deepseek-r1:32") || n.Contains("32b"))
                model.Settings = OllamaPresets.DeepSeekR1_32B_Reason;
            else if (n.StartsWith("deepseek-r1:14") || n.Contains("14b"))
                model.Settings = OllamaPresets.DeepSeekR1_14B_Reason;
            else if (n.Contains("mistral") && (n.Contains("7b") || !n.Contains("large")))
                model.Settings = OllamaPresets.Mistral7BGeneral;
            else if (n.StartsWith("qwen2.5") || n.Contains("qwen"))
                model.Settings = OllamaPresets.Qwen25_7B_General;
            else if (n.StartsWith("phi3") || n.Contains("phi-3"))
                model.Settings = OllamaPresets.Phi3MiniGeneral;
        }
        catch
        {
            // Non-fatal: preset mapping is best-effort.
        }
    }
}
