// <copyright file="AvatarVideoGenerator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Application.Avatar;

/// <summary>
/// Generates avatar video frames via Ollama's Stable Diffusion img2img endpoint.
/// The existing Iaret portrait asset is used as the seed image.
/// </summary>
public sealed class AvatarVideoGenerator
{
    private readonly HttpClient _http;
    private readonly string _sdModel;
    private readonly ILogger<AvatarVideoGenerator>? _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="AvatarVideoGenerator"/> class.
    /// </summary>
    /// <param name="ollamaEndpoint">Base URL of the Ollama server.</param>
    /// <param name="sdModel">Stable Diffusion model name registered in Ollama.</param>
    /// <param name="logger">Optional logger.</param>
    public AvatarVideoGenerator(
        string ollamaEndpoint = "http://localhost:11434",
        string sdModel = "stable-diffusion",
        ILogger<AvatarVideoGenerator>? logger = null)
    {
        _sdModel = sdModel;
        _logger = logger;
        _http = new HttpClient
        {
            BaseAddress = new Uri(ollamaEndpoint.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(60),
        };
    }

    /// <summary>
    /// Builds an emotional prompt from the current avatar state snapshot.
    /// The prompt is tailored to Iaret's visual state and mood to guide SD generation.
    /// </summary>
    public static string BuildPrompt(AvatarStateSnapshot state)
    {
        var basePrompt = state.VisualState switch
        {
            AvatarVisualState.Idle =>
                "Egyptian goddess Iaret, regal serpent deity, composed expression, golden crown, ambient light, photorealistic portrait",
            AvatarVisualState.Listening =>
                "Egyptian goddess Iaret, attentive gaze, leaning forward slightly, warm expression, photorealistic",
            AvatarVisualState.Thinking =>
                "Egyptian goddess Iaret, contemplative expression, glowing aura, holographic patterns, photorealistic",
            AvatarVisualState.Speaking =>
                "Egyptian goddess Iaret, speaking with authority, animated expression, golden light, photorealistic",
            AvatarVisualState.Encouraging =>
                "Egyptian goddess Iaret, gentle maternal smile, warm golden light, nurturing expression, photorealistic",
            _ =>
                "Egyptian goddess Iaret, regal serpent deity, photorealistic portrait",
        };

        // Append mood modifiers
        var moodModifier = state.Mood?.ToLowerInvariant() switch
        {
            "warm" or "happy" => ", soft warm lighting",
            "resolute" or "determined" => ", strong confident expression",
            "curious" or "intrigued" => ", inquisitive gaze, subtle light shift",
            "calm" or "serene" => ", peaceful atmosphere, gentle diffused light",
            "concerned" or "worried" => ", slight furrowed brow, cooler light tones",
            "excited" or "enthusiastic" => ", vibrant golden aura, energetic expression",
            "sad" or "melancholic" => ", subdued lighting, thoughtful distant gaze",
            "neutral" => string.Empty,
            _ => string.Empty,
        };

        return basePrompt + moodModifier;
    }

    /// <summary>
    /// Calls Ollama's /api/generate with the SD model, prompt, and seed image (img2img).
    /// Returns a base64-encoded JPEG frame, or null on failure.
    /// </summary>
    /// <param name="prompt">Text prompt describing the desired avatar frame.</param>
    /// <param name="seedBase64">Base64-encoded seed image (existing Iaret portrait).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Base64-encoded JPEG of the generated frame, or null on error.</returns>
    public async Task<string?> GenerateFrameAsync(string prompt, string seedBase64, CancellationToken ct)
    {
        try
        {
            var payload = new
            {
                model = _sdModel,
                prompt,
                images = new[] { seedBase64 },
                stream = false,
            };

            var json = JsonSerializer.Serialize(payload, JsonOpts);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync("/api/generate", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Ollama SD returned {StatusCode}: {Reason}",
                    response.StatusCode, response.ReasonPhrase);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            // The response may contain "images" array with base64 frames
            if (doc.RootElement.TryGetProperty("images", out var imagesElement)
                && imagesElement.ValueKind == JsonValueKind.Array
                && imagesElement.GetArrayLength() > 0)
            {
                return imagesElement[0].GetString();
            }

            // Alternative response format: single "image" property
            if (doc.RootElement.TryGetProperty("image", out var imageElement)
                && imageElement.ValueKind == JsonValueKind.String)
            {
                return imageElement.GetString();
            }

            _logger?.LogWarning("Ollama SD response did not contain image data");
            return null;
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate avatar frame via Ollama SD");
            return null;
        }
    }

    /// <summary>
    /// Selects the correct seed asset file path based on the current visual state.
    /// </summary>
    /// <param name="state">Current avatar visual state.</param>
    /// <param name="assetDirectory">Root avatar asset directory (containing Iaret subfolder).</param>
    /// <returns>Full path to the seed image file.</returns>
    public static string GetSeedAssetPath(AvatarVisualState state, string assetDirectory)
    {
        var filename = state switch
        {
            AvatarVisualState.Idle => "idle.png",
            AvatarVisualState.Listening => "listening.png",
            AvatarVisualState.Thinking => "thinking.png",
            AvatarVisualState.Speaking => "speaking.png",
            AvatarVisualState.Encouraging => "encouraging.png",
            _ => "idle.png",
        };

        var iaretDir = Path.Combine(assetDirectory, "Iaret");
        var path = Path.Combine(iaretDir, filename);

        // Fallback to idle if specific state image doesn't exist
        if (!File.Exists(path))
        {
            path = Path.Combine(iaretDir, "idle.png");
        }

        return path;
    }
}
