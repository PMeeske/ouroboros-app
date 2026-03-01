using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Android.Services;

/// <summary>
/// Unified AI service supporting multiple providers
/// </summary>
public partial class UnifiedAIService
{
    private readonly AIProviderService _providerService;
    private readonly HttpClient _httpClient;
    private readonly SymbolicReasoningEngine _reasoningEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedAIService"/> class.
    /// </summary>
    public UnifiedAIService()
    {
        _providerService = new AIProviderService();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _reasoningEngine = new SymbolicReasoningEngine();
    }

    /// <summary>
    /// Gets the symbolic reasoning engine
    /// </summary>
    public SymbolicReasoningEngine ReasoningEngine => _reasoningEngine;

    /// <summary>
    /// Generate a response using the active provider
    /// </summary>
    public async Task<string> GenerateAsync(
        string prompt,
        string? model = null,
        Action<string>? streamCallback = null,
        CancellationToken cancellationToken = default)
    {
        var config = _providerService.GetActiveProviderConfig();
        var useModel = model ?? config.DefaultModel ?? "default";

        return config.Provider switch
        {
            AIProvider.Ollama => await GenerateOllamaAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.OpenAI => await GenerateOpenAIAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.Anthropic => await GenerateAnthropicAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.Google => await GenerateGoogleAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.Meta => await GenerateMetaAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.Cohere => await GenerateCohereAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.Mistral => await GenerateMistralAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.HuggingFace => await GenerateHuggingFaceAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.AzureOpenAI => await GenerateAzureOpenAIAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.GitHubModels => await GenerateOpenAIAsync(config, useModel, prompt, streamCallback, cancellationToken),
            _ => throw new NotSupportedException($"Provider {config.Provider} not supported")
        };
    }

    /// <summary>
    /// Generate with symbolic reasoning augmentation
    /// </summary>
    public async Task<string> GenerateWithReasoningAsync(
        string prompt,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        // First, perform symbolic reasoning on the prompt
        var reasoningContext = _reasoningEngine.ExportKnowledgeBase();
        
        // Augment prompt with reasoning context if knowledge base is not empty
        var augmentedPrompt = prompt;
        if (_reasoningEngine.GetAllFacts().Count > 0)
        {
            augmentedPrompt = $@"Knowledge Base Context:
{reasoningContext}

User Query:
{prompt}

Please use the knowledge base context above to inform your response.";
        }

        return await GenerateAsync(augmentedPrompt, model, null, cancellationToken);
    }

    private async Task<string> GenerateOllamaAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            model = model,
            prompt = prompt,
            stream = streamCallback != null,
            options = new
            {
                temperature = config.Temperature ?? 0.7,
                num_predict = config.MaxTokens ?? 2000
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        ConfigureHeaders(config);

        using var response = await _httpClient.PostAsync(
            $"{config.Endpoint}/api/generate",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        if (streamCallback != null)
        {
            return await ProcessStreamingResponse(response, streamCallback, cancellationToken);
        }
        else
        {
            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);
            return result?.Response ?? string.Empty;
        }
    }

    private async Task<string> GenerateOpenAIAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            model = model,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = config.Temperature ?? 0.7,
            max_tokens = config.MaxTokens ?? 2000,
            stream = streamCallback != null
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        ConfigureHeaders(config);

        using var response = await _httpClient.PostAsync(
            $"{config.Endpoint}/chat/completions",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        if (streamCallback != null)
        {
            return await ProcessOpenAIStreamingResponse(response, streamCallback, cancellationToken);
        }
        else
        {
            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(result);
            return jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }
    }

    private async Task<string> GenerateAnthropicAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            model = model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = config.MaxTokens ?? 2000,
            temperature = config.Temperature ?? 0.7,
            stream = streamCallback != null
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        ConfigureHeaders(config);
        if (!_httpClient.DefaultRequestHeaders.Contains("anthropic-version"))
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        using var response = await _httpClient.PostAsync(
            $"{config.Endpoint}/messages",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(result);
        return jsonDoc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    private async Task<string> GenerateGoogleAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = config.Temperature ?? 0.7,
                maxOutputTokens = config.MaxTokens ?? 2000
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var url = $"{config.Endpoint}/models/{model}:generateContent?key={config.ApiKey}";

        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(result);
        return jsonDoc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    private async Task<string> GenerateMetaAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        // Meta LLaMA via Together.ai or similar API
        return await GenerateOpenAIAsync(config, model, prompt, streamCallback, cancellationToken);
    }

}
