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
/// Generates avatar video frames via the Forge/AUTOMATIC1111 Stable Diffusion img2img API.
/// The existing Iaret portrait asset is used as the seed image.
/// </summary>
public sealed class AvatarVideoGenerator
{
    private readonly HttpClient _http;
    private readonly string? _sdCheckpoint;
    private readonly ILogger<AvatarVideoGenerator>? _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="AvatarVideoGenerator"/> class.
    /// </summary>
    /// <param name="sdEndpoint">Base URL of the Forge/A1111 server (default: http://localhost:7860).</param>
    /// <param name="sdModel">Optional checkpoint name to load via override_settings. Leave null/empty to use whichever model is currently loaded.</param>
    /// <param name="timeoutSeconds">HTTP request timeout in seconds (default: 300). SD img2img can take several minutes on slower hardware.</param>
    /// <param name="logger">Optional logger.</param>
    public AvatarVideoGenerator(
        string sdEndpoint = "http://localhost:7860",
        string? sdModel = null,
        int timeoutSeconds = 300,
        ILogger<AvatarVideoGenerator>? logger = null)
    {
        _sdCheckpoint = string.IsNullOrWhiteSpace(sdModel) || sdModel == "stable-diffusion"
            ? null
            : sdModel;
        _logger = logger;
        _http = new HttpClient
        {
            BaseAddress = new Uri(sdEndpoint.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
        };
    }

    /// <summary>
    /// Builds an emotional prompt from the current avatar state snapshot.
    /// The prompt is tailored to Iaret's visual state and mood to guide SD generation.
    /// </summary>
    public static string BuildPrompt(AvatarStateSnapshot state)
    {
        // Focus only on the facial expression change — avoid re-describing the full
        // scene so that img2img with low denoising_strength doesn't drift appearance.
        var expressionPrompt = state.VisualState switch
        {
            AvatarVisualState.Idle =>
                "neutral composed expression, relaxed face, eyes forward",
            AvatarVisualState.Listening =>
                "attentive listening expression, soft focused gaze, slightly raised brows",
            AvatarVisualState.Thinking =>
                "contemplative expression, eyes slightly upward, subtle furrowed brow",
            AvatarVisualState.Speaking =>
                "speaking expression, slightly open mouth, animated face, engaged eyes",
            AvatarVisualState.Encouraging =>
                "warm gentle smile, kind eyes, soft encouraging expression",
            _ =>
                "neutral expression",
        };

        // Append mood as a micro-expression modifier only
        var moodModifier = state.Mood?.ToLowerInvariant() switch
        {
            "warm" or "happy" => ", happy eyes, slight smile",
            "resolute" or "determined" => ", firm set jaw, confident gaze",
            "curious" or "intrigued" => ", curious raised eyebrow, interested look",
            "calm" or "serene" => ", peaceful relaxed face",
            "concerned" or "worried" => ", slight worried frown",
            "excited" or "enthusiastic" => ", bright wide eyes, energetic expression",
            "sad" or "melancholic" => ", sad eyes, downward gaze",
            _ => string.Empty,
        };

        return expressionPrompt + moodModifier;
    }

    /// <summary>
    /// Loads the configured SD checkpoint via /sdapi/v1/options (once at startup).
    /// Must be awaited before the first <see cref="GenerateFrameAsync"/> call.
    /// </summary>
    public async Task LoadCheckpointAsync(CancellationToken ct = default)
    {
        if (_sdCheckpoint == null) return;
        try
        {
            var body = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["sd_model_checkpoint"] = _sdCheckpoint,
            });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync("/sdapi/v1/options", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                var msg = $"Could not set SD checkpoint '{_sdCheckpoint}': {(int)response.StatusCode} {err}";
                _logger?.LogWarning("{Message}", msg);
                if (_logger == null) Console.Error.WriteLine($"[AvatarVideoGenerator] {msg}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to set SD checkpoint");
            if (_logger == null) Console.Error.WriteLine($"[AvatarVideoGenerator] Failed to set SD checkpoint: {ex.Message}");
        }
    }

    /// <summary>
    /// Calls the Forge/A1111 /sdapi/v1/img2img endpoint with the prompt and seed image.
    /// Returns a base64-encoded JPEG frame, or null on failure.
    /// </summary>
    /// <param name="prompt">Text prompt describing the desired avatar frame.</param>
    /// <param name="seedBase64">Base64-encoded seed image (existing Iaret portrait).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Base64-encoded image of the generated frame, or null on error.</returns>
    public async Task<string?> GenerateFrameAsync(string prompt, string seedBase64, CancellationToken ct)
    {
        try
        {
            // Expression-only img2img: very low denoising_strength (0.15) so only subtle
            // facial expression changes are applied, preserving Iaret's full appearance.
            // Checkpoint is loaded once via LoadCheckpointAsync — never per-request.
            var payloadObj = new Dictionary<string, object?>
            {
                ["prompt"] = prompt,
                ["negative_prompt"] = "ugly, blurry, low quality, deformed, disfigured, extra limbs, changed hair, different person, different clothes, different background, style change, color change",
                ["init_images"] = new[] { seedBase64 },
                ["denoising_strength"] = 0.15,
                ["steps"] = 8,
                ["cfg_scale"] = 5,
                ["width"] = 384,
                ["height"] = 512,
                ["sampler_name"] = "Euler",
            };

            var json = JsonSerializer.Serialize(payloadObj);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync("/sdapi/v1/img2img", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                var msg = $"Forge SD returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}";
                _logger?.LogWarning("{Message}", msg);
                if (_logger == null) Console.Error.WriteLine($"[AvatarVideoGenerator] {msg}");
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            // A1111/Forge returns {"images": ["base64..."]}
            if (doc.RootElement.TryGetProperty("images", out var imagesElement)
                && imagesElement.ValueKind == JsonValueKind.Array
                && imagesElement.GetArrayLength() > 0)
            {
                return imagesElement[0].GetString();
            }

            const string noData = "Forge SD response did not contain image data";
            _logger?.LogWarning(noData);
            if (_logger == null) Console.Error.WriteLine($"[AvatarVideoGenerator] {noData}");
            return null;
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate avatar frame via Forge SD");
            if (_logger == null) Console.Error.WriteLine($"[AvatarVideoGenerator] Frame generation failed: {ex.Message}");
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
