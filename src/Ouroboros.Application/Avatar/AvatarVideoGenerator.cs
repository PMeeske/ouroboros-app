// <copyright file="AvatarVideoGenerator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
    /// Only the face region (top 48%) is sent to SD; the result is composited back onto
    /// the original image with a feathered seam so the body and background are untouched.
    /// Returns a base64-encoded JPEG frame, or null on failure.
    /// </summary>
    /// <param name="prompt">Text prompt describing the desired avatar frame.</param>
    /// <param name="seedBase64">Base64-encoded seed image (existing Iaret portrait).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Base64-encoded JPEG of the composited frame, or null on error.</returns>
    public async Task<string?> GenerateFrameAsync(string prompt, string seedBase64, CancellationToken ct)
    {
        try
        {
            // ── 1. Decode seed image ──────────────────────────────────────────────
            byte[] seedBytes = Convert.FromBase64String(seedBase64);
            using var seedMs = new MemoryStream(seedBytes);
            using var seedBmp = new Bitmap(seedMs);

            // ── 2. Face crop: top 48 %, width snapped to multiple of 8 for SD ────
            int faceH = (int)(seedBmp.Height * 0.48) / 8 * 8;
            var faceRect = new Rectangle(0, 0, seedBmp.Width, faceH);
            using var faceBmp = (Bitmap)seedBmp.Clone(faceRect, seedBmp.PixelFormat);

            string faceSeedBase64;
            using (var faceMs = new MemoryStream())
            {
                faceBmp.Save(faceMs, ImageFormat.Png);
                faceSeedBase64 = Convert.ToBase64String(faceMs.ToArray());
            }

            // ── 3. Send just the face crop to Forge SD ───────────────────────────
            var payloadObj = new Dictionary<string, object?>
            {
                ["prompt"] = prompt,
                ["negative_prompt"] = "ugly, blurry, low quality, deformed, disfigured, extra limbs, changed hair, different person, different clothes, different background, style change, color change",
                ["init_images"] = new[] { faceSeedBase64 },
                ["denoising_strength"] = 0.22,
                ["steps"] = 8,
                ["cfg_scale"] = 5,
                ["width"] = seedBmp.Width,
                ["height"] = faceH,
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

            if (!doc.RootElement.TryGetProperty("images", out var imagesElement)
                || imagesElement.ValueKind != JsonValueKind.Array
                || imagesElement.GetArrayLength() == 0)
            {
                const string noData = "Forge SD response did not contain image data";
                _logger?.LogWarning(noData);
                if (_logger == null) Console.Error.WriteLine($"[AvatarVideoGenerator] {noData}");
                return null;
            }

            string? faceResultBase64 = imagesElement[0].GetString();
            if (faceResultBase64 == null) return null;

            // ── 4. Decode SD face result ──────────────────────────────────────────
            byte[] faceResultBytes = Convert.FromBase64String(faceResultBase64);
            using var faceResultMs = new MemoryStream(faceResultBytes);
            using var faceResultBmp = new Bitmap(faceResultMs);

            // ── 5. Composite: original base + SD face with feathered seam ─────────
            //   Layout (seedBmp.Height total):
            //     [0 .. solidH)        → SD face result (opaque)
            //     [solidH .. faceH)    → feathered blend (SD fades to original)
            //     [faceH .. height)    → original body (untouched)
            //
            //   The blend zone is the bottom 22 % of the face crop, divided into
            //   16 thin strips each with linearly decreasing alpha.
            int blendZoneH = faceH * 22 / 100;
            int solidH = faceH - blendZoneH;
            const int BlendSteps = 16;
            int stripH = Math.Max(1, blendZoneH / BlendSteps);

            using var composite = new Bitmap(seedBmp.Width, seedBmp.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(composite);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;

            // Base layer: full original
            g.DrawImage(seedBmp, 0, 0, seedBmp.Width, seedBmp.Height);

            // Solid SD face region
            g.DrawImage(
                faceResultBmp,
                new Rectangle(0, 0, seedBmp.Width, solidH),
                new RectangleF(0, 0, faceResultBmp.Width, (float)solidH / faceH * faceResultBmp.Height),
                GraphicsUnit.Pixel);
    
            // Feathered blend strips
            using var ia = new ImageAttributes();
            for (int i = 0; i < BlendSteps; i++)
            {
                float alpha = 1.0f - (float)(i + 1) / BlendSteps; // 1.0 → ~0.0
                int dstY = solidH + i * stripH;
                int dstH = (i == BlendSteps - 1) ? (faceH - dstY) : stripH; // last strip fills gap
                if (dstH <= 0) continue;

                float srcY = (float)dstY / faceH * faceResultBmp.Height;
                float srcH = (float)dstH / faceH * faceResultBmp.Height;

                var cm = new ColorMatrix { Matrix33 = alpha };
                ia.SetColorMatrix(cm);

                g.DrawImage(
                    faceResultBmp,
                    new Rectangle(0, dstY, seedBmp.Width, dstH),
                    0, srcY, faceResultBmp.Width, srcH,
                    GraphicsUnit.Pixel,
                    ia);
            }

            // ── 6. Encode composite as JPEG ───────────────────────────────────────
            using var outMs = new MemoryStream();
            composite.Save(outMs, ImageFormat.Jpeg);
            return Convert.ToBase64String(outMs.ToArray());
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
