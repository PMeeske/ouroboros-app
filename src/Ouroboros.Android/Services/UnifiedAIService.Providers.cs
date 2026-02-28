using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Android.Services;

/// <summary>
/// Additional provider implementations, header configuration, and streaming support.
/// </summary>
public partial class UnifiedAIService
{
    private async Task<string> GenerateCohereAsync(
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
            max_tokens = config.MaxTokens ?? 2000,
            temperature = config.Temperature ?? 0.7,
            stream = streamCallback != null
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        ConfigureHeaders(config);

        using var response = await _httpClient.PostAsync(
            $"{config.Endpoint}/generate",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(result);
        return jsonDoc.RootElement
            .GetProperty("generations")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    private async Task<string> GenerateMistralAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        // Mistral uses OpenAI-compatible API
        return await GenerateOpenAIAsync(config, model, prompt, streamCallback, cancellationToken);
    }

    private async Task<string> GenerateHuggingFaceAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            inputs = prompt,
            parameters = new
            {
                max_new_tokens = config.MaxTokens ?? 2000,
                temperature = config.Temperature ?? 0.7
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        ConfigureHeaders(config);

        using var response = await _httpClient.PostAsync(
            $"{config.Endpoint}/models/{model}",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(result);
        return jsonDoc.RootElement[0]
            .GetProperty("generated_text")
            .GetString() ?? string.Empty;
    }

    private async Task<string> GenerateAzureOpenAIAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
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

        var url = $"{config.Endpoint}/openai/deployments/{config.DeploymentName}/chat/completions?api-version=2023-05-15";

        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
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

    private void ConfigureHeaders(AIProviderConfig config)
    {
        _httpClient.DefaultRequestHeaders.Clear();

        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        if (!string.IsNullOrEmpty(config.OrganizationId))
        {
            _httpClient.DefaultRequestHeaders.Add("OpenAI-Organization", config.OrganizationId);
        }

        foreach (var (key, value) in config.CustomHeaders)
        {
            _httpClient.DefaultRequestHeaders.Add(key, value);
        }
    }

    private async Task<string> ProcessStreamingResponse(
        HttpResponseMessage response,
        Action<string> callback,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var fullResponse = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (!string.IsNullOrEmpty(line))
            {
                try
                {
                    var jsonResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(line);
                    if (jsonResponse?.Response != null)
                    {
                        fullResponse.Append(jsonResponse.Response);
                        callback(jsonResponse.Response);
                    }

                    if (jsonResponse?.Done == true)
                    {
                        break;
                    }
                }
                catch
                {
                    // Skip malformed JSON lines
                }
            }
        }

        return fullResponse.ToString();
    }

    private async Task<string> ProcessOpenAIStreamingResponse(
        HttpResponseMessage response,
        Action<string> callback,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var fullResponse = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line?.StartsWith("data: ") == true)
            {
                var data = line.Substring(6);

                if (data == "[DONE]")
                {
                    break;
                }

                try
                {
                    var jsonDoc = JsonDocument.Parse(data);
                    var delta = jsonDoc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("delta");

                    if (delta.TryGetProperty("content", out var content))
                    {
                        var text = content.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            fullResponse.Append(text);
                            callback(text);
                        }
                    }
                }
                catch
                {
                    // Skip malformed JSON lines
                }
            }
        }

        return fullResponse.ToString();
    }
}
