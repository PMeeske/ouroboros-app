// <copyright file="VisionService.Backend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Net.Http;
using System.Text;
using System.Text.Json;

/// <summary>
/// Backend communication methods for Ollama and OpenAI vision APIs.
/// </summary>
public partial class VisionService
{
    private async Task<VisionResult> AnalyzeBase64ImageAsync(string base64Image, string mimeType, string? prompt, CancellationToken ct)
    {
        prompt ??= "Describe what you see in this image in detail.";

        try
        {
            return _config.Backend switch
            {
                VisionBackend.Ollama => await AnalyzeWithOllamaAsync(base64Image, prompt, ct),
                VisionBackend.OpenAI => await AnalyzeWithOpenAIAsync(base64Image, mimeType, prompt, ct),
                _ => await AnalyzeWithOllamaAsync(base64Image, prompt, ct),
            };
        }
        catch (Exception ex)
        {
            return VisionResult.Failure($"Vision analysis failed: {ex.Message}");
        }
    }

    private async Task<VisionResult> AnalyzeWithOllamaAsync(string base64Image, string prompt, CancellationToken ct)
    {
        var requestBody = new
        {
            model = _config.OllamaVisionModel,
            prompt = prompt,
            images = new[] { base64Image },
            stream = false,
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_config.OllamaEndpoint}/api/generate", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return VisionResult.Failure($"Ollama error: {error}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

        var description = responseObj.GetProperty("response").GetString() ?? "";

        var result = new VisionResult
        {
            Success = true,
            Description = description,
            Timestamp = DateTime.Now,
            AnalysisType = "image_description",
            Model = _config.OllamaVisionModel,
        };

        OnVisionResult?.Invoke(result);
        return result;
    }

    private async Task<VisionResult> AnalyzeWithOpenAIAsync(string base64Image, string mimeType, string prompt, CancellationToken ct)
    {
        var requestBody = new
        {
            model = _config.OpenAIVisionModel,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } },
                    },
                },
            },
            max_tokens = 1000,
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.OpenAIApiKey);
        request.Content = content;

        var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return VisionResult.Failure($"OpenAI error: {error}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

        var description = responseObj
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        var result = new VisionResult
        {
            Success = true,
            Description = description,
            Timestamp = DateTime.Now,
            AnalysisType = "image_description",
            Model = _config.OpenAIVisionModel,
        };

        OnVisionResult?.Invoke(result);
        return result;
    }

    private async Task<string> AnalyzeTextAsync(string prompt, CancellationToken ct)
    {
        var requestBody = new
        {
            model = _config.OllamaVisionModel.Replace("llava", "llama3.2"),
            prompt = prompt,
            stream = false,
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_config.OllamaEndpoint}/api/generate", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            return "";
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

        return responseObj.GetProperty("response").GetString() ?? "";
    }
}
