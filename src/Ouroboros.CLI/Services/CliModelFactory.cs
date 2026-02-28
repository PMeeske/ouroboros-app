using LangChain.Providers.Ollama;
using Ouroboros.Providers;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Centralised factory for creating CLI-level LLM and embedding models.
/// Consolidates the 5+ duplicated Ollama initialization blocks and model-preset
/// detection if-chains that previously appeared across AskCommands, MeTTaService,
/// RoomMode, and ImmersiveMode.
/// </summary>
public static class CliModelFactory
{
    // ── Model preset detection ──────────────────────────────────────────────

    /// <summary>
    /// Applies the appropriate <see cref="OllamaPresets"/> to a local <see cref="OllamaChatModel"/>
    /// based on its model name.  Best-effort — silently ignored on parse failure.
    /// </summary>
    public static void ApplyModelPreset(OllamaChatModel model, string modelName)
    {
        try
        {
            string n = (modelName ?? string.Empty).ToLowerInvariant();
            if      (n.StartsWith("deepseek-coder:33b"))                       model.Settings = OllamaPresets.DeepSeekCoder33B;
            else if (n.StartsWith("llama3"))                                   model.Settings = OllamaPresets.Llama3General;
            else if (n.StartsWith("deepseek-r1:32") || n.Contains("32b"))     model.Settings = OllamaPresets.DeepSeekR1_32B_Reason;
            else if (n.StartsWith("deepseek-r1:14") || n.Contains("14b"))     model.Settings = OllamaPresets.DeepSeekR1_14B_Reason;
            else if (n.Contains("mistral") && (n.Contains("7b") || !n.Contains("large")))
                                                                               model.Settings = OllamaPresets.Mistral7BGeneral;
            else if (n.StartsWith("qwen2.5") || n.Contains("qwen"))           model.Settings = OllamaPresets.Qwen25_7B_General;
            else if (n.StartsWith("phi3") || n.Contains("phi-3"))             model.Settings = OllamaPresets.Phi3MiniGeneral;
        }
        catch
        {
            // Non-fatal: preset mapping is best-effort. Provider defaults are fine if detection fails.
        }
    }

    /// <summary>
    /// Applies a role-specific preset to a local model (used in multi-model router paths).
    /// Includes role overrides: "summarize" gets the summarize preset for llama3.
    /// </summary>
    public static void ApplyModelPreset(OllamaChatModel model, string modelName, string role)
    {
        try
        {
            string n = (modelName ?? string.Empty).ToLowerInvariant();
            if (n.StartsWith("llama3"))
            {
                model.Settings = role.Equals("summarize", StringComparison.OrdinalIgnoreCase)
                    ? OllamaPresets.Llama3Summarize
                    : OllamaPresets.Llama3General;
            }
            else
            {
                ApplyModelPreset(model, modelName);
            }
        }
        catch (InvalidOperationException) { /* best-effort preset application */ }
    }

    // ── Embedding model ─────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to create an embedding model; returns null on failure.
    /// Consolidates the try-catch wrapping repeated across 5+ call sites.
    /// </summary>
    public static IEmbeddingModel? TryCreateEmbeddingModel(
        string? endpoint,
        string? apiKey,
        ChatEndpointType endpointType,
        string embedModelName)
    {
        try
        {
            var provider = new OllamaProvider();
            return Ouroboros.Application.Services.ServiceFactory
                .CreateEmbeddingModel(endpoint, apiKey, endpointType, embedModelName, provider);
        }
        catch
        {
            return null;
        }
    }
}
