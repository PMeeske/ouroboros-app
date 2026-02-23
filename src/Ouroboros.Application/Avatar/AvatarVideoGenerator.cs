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
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Application.Avatar;

/// <summary>
/// Generates avatar video frames via Stability AI cloud API or local Forge/AUTOMATIC1111 img2img.
/// The existing Iaret portrait asset is used as the seed image.
/// On error the pipeline falls through: gatekeeper → corrector → CSS fallback (null).
/// </summary>
public sealed class AvatarVideoGenerator
{
    private readonly HttpClient _http;
    private readonly HttpClient? _stabilityHttp;
    private readonly string? _sdCheckpoint;
    private readonly string? _stabilityAiApiKey;
    private readonly string _stabilityModel;
    private readonly double _stabilityStrength;
    private readonly ILogger<AvatarVideoGenerator>? _logger;
    private readonly IVisionModel? _visionModel;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Gets whether the generator is configured with a Stability AI cloud backend.</summary>
    public bool IsCloudEnabled => !string.IsNullOrEmpty(_stabilityAiApiKey);

    /// <summary>
    /// Initializes a new instance of the <see cref="AvatarVideoGenerator"/> class.
    /// </summary>
    /// <param name="sdEndpoint">Base URL of the Forge/A1111 server (default: http://localhost:7860).</param>
    /// <param name="sdModel">Optional checkpoint name to load via override_settings. Leave null/empty to use whichever model is currently loaded.</param>
    /// <param name="timeoutSeconds">HTTP request timeout in seconds (default: 300). SD img2img can take several minutes on slower hardware.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="visionModel">Optional vision model (Qwen VL) used for gatekeeper and corrector stages.</param>
    /// <param name="stabilityAiApiKey">Optional Stability AI API key. When set, the cloud backend is used instead of local Forge.</param>
    /// <param name="stabilityModel">Stability AI model (default: sd3.5-medium).</param>
    /// <param name="stabilityStrength">Image-to-image strength for Stability AI (0-1, default: 0.35).</param>
    public AvatarVideoGenerator(
        string sdEndpoint = "http://localhost:7860",
        string? sdModel = null,
        int timeoutSeconds = 300,
        ILogger<AvatarVideoGenerator>? logger = null,
        IVisionModel? visionModel = null,
        string? stabilityAiApiKey = null,
        string stabilityModel = "sd3.5-medium",
        double stabilityStrength = 0.35)
    {
        _sdCheckpoint = string.IsNullOrWhiteSpace(sdModel) || sdModel == "stable-diffusion"
            ? null
            : sdModel;
        _logger = logger;
        _visionModel = visionModel;
        _stabilityAiApiKey = stabilityAiApiKey;
        _stabilityModel = stabilityModel;
        _stabilityStrength = stabilityStrength;
        _http = new HttpClient
        {
            BaseAddress = new Uri(sdEndpoint.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
        };

        if (!string.IsNullOrEmpty(stabilityAiApiKey))
        {
            _stabilityHttp = new HttpClient
            {
                BaseAddress = new Uri("https://api.stability.ai"),
                Timeout = TimeSpan.FromSeconds(60),
            };
            _stabilityHttp.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", stabilityAiApiKey);
            _stabilityHttp.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }
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
    /// Generates a composited avatar frame.
    /// Pipeline: SD face crop → Gatekeeper (Qwen VL) → Corrector (Qwen VL) → CSS fallback (null).
    /// Only the face region (top 48 %) is sent to SD; the result is blended back onto the
    /// full seed image with a feathered seam so body and background are never touched.
    /// </summary>
    public async Task<string?> GenerateFrameAsync(string prompt, string seedBase64, CancellationToken ct)
    {
        try
        {
            // ── 1. Decode seed ────────────────────────────────────────────────────
            byte[] seedBytes = Convert.FromBase64String(seedBase64);
            using var seedMs = new MemoryStream(seedBytes);
            using var seedBmp = new Bitmap(seedMs);

            // ── 2. Face crop: top 48 %, snapped to multiple of 8 for SD ──────────
            int faceH = (int)(seedBmp.Height * 0.48) / 8 * 8;
            var faceRect = new Rectangle(0, 0, seedBmp.Width, faceH);
            using var faceBmp = (Bitmap)seedBmp.Clone(faceRect, seedBmp.PixelFormat);

            // Encode as 24bpp JPEG — avoids Windows PNG ICC metadata that Forge PIL rejects
            string faceSeedBase64;
            using (var rgb = new Bitmap(faceBmp.Width, faceBmp.Height, PixelFormat.Format24bppRgb))
            {
                using (var gRgb = Graphics.FromImage(rgb))
                    gRgb.DrawImage(faceBmp, 0, 0, faceBmp.Width, faceBmp.Height);
                using var faceMs = new MemoryStream();
                rgb.Save(faceMs, ImageFormat.Jpeg);
                faceSeedBase64 = Convert.ToBase64String(faceMs.ToArray());
            }

            // ── 3. SD call (cloud or local) ──────────────────────────────────────
            string? faceResultBase64 = _stabilityHttp != null
                ? await CallStabilityAiFaceAsync(prompt, faceSeedBase64, ct)
                : await CallSdFaceAsync(prompt, faceSeedBase64, seedBmp.Width, faceH, ct);
            if (faceResultBase64 == null) return null; // Fallback: CSS layers

            // ── 4. Gatekeeper — Qwen VL verifies the expression is correct ────────
            if (_visionModel != null)
            {
                byte[] faceResultBytes = Convert.FromBase64String(faceResultBase64);
                string expressionLabel = prompt.Split(',')[0].Trim();

                var gate = await _visionModel.AnswerQuestionAsync(
                    faceResultBytes, "jpeg",
                    $"Does this face clearly show a {expressionLabel}? Answer only yes or no.",
                    ct);

                bool passed = gate.IsSuccess &&
                              gate.Value.Trim().StartsWith("yes", StringComparison.OrdinalIgnoreCase);

                if (!passed)
                {
                    // ── 5. Corrector — ask Qwen for a better prompt, retry SD ─────
                    var fix = await _visionModel.AnswerQuestionAsync(
                        faceResultBytes, "jpeg",
                        $"Target expression: {prompt}. What does this face actually show and what 1-line expression prompt would correct it? Output only the corrected prompt.",
                        ct);

                    if (fix.IsSuccess)
                    {
                        string correctedPrompt = fix.Value.Trim();
                        faceResultBase64 = _stabilityHttp != null
                            ? await CallStabilityAiFaceAsync(correctedPrompt, faceSeedBase64, ct)
                            : await CallSdFaceAsync(correctedPrompt, faceSeedBase64, seedBmp.Width, faceH, ct);
                        if (faceResultBase64 == null) return null; // Fallback: CSS layers
                    }
                    else
                    {
                        return null; // Fallback: CSS layers
                    }
                }
            }

            // ── 6. Composite: SD face + feathered seam + original body ────────────
            byte[] resultBytes = Convert.FromBase64String(faceResultBase64);
            using var faceResultMs = new MemoryStream(resultBytes);
            using var faceResultBmp = new Bitmap(faceResultMs);

            int blendZoneH = faceH * 22 / 100;
            int solidH = faceH - blendZoneH;
            const int BlendSteps = 16;
            int stripH = Math.Max(1, blendZoneH / BlendSteps);

            using var composite = new Bitmap(seedBmp.Width, seedBmp.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(composite);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;

            g.DrawImage(seedBmp, 0, 0, seedBmp.Width, seedBmp.Height); // base: full original

            g.DrawImage(                                                // solid face region
                faceResultBmp,
                new Rectangle(0, 0, seedBmp.Width, solidH),
                new RectangleF(0, 0, faceResultBmp.Width, (float)solidH / faceH * faceResultBmp.Height),
                GraphicsUnit.Pixel);

            using var ia = new ImageAttributes();
            for (int i = 0; i < BlendSteps; i++)                       // feathered seam
            {
                float alpha = 1.0f - (float)(i + 1) / BlendSteps;
                int dstY = solidH + i * stripH;
                int dstH = i == BlendSteps - 1 ? faceH - dstY : stripH;
                if (dstH <= 0) continue;

                ia.SetColorMatrix(new ColorMatrix { Matrix33 = alpha });
                g.DrawImage(faceResultBmp,
                    new Rectangle(0, dstY, seedBmp.Width, dstH),
                    0, (float)dstY / faceH * faceResultBmp.Height,
                    faceResultBmp.Width, (float)dstH / faceH * faceResultBmp.Height,
                    GraphicsUnit.Pixel, ia);
            }

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
            _logger?.LogWarning(ex, "Failed to generate avatar frame");
            if (_logger == null) Console.Error.WriteLine($"[AvatarVideoGenerator] Frame generation failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sends a face crop to the Stability AI v2beta cloud API for img2img generation.
    /// Returns base64-encoded JPEG on success, null on failure (→ CSS fallback).
    /// </summary>
    private async Task<string?> CallStabilityAiFaceAsync(string prompt, string faceSeedBase64, CancellationToken ct)
    {
        byte[] imageBytes = Convert.FromBase64String(faceSeedBase64);

        // Build multipart body manually — .NET's MultipartFormDataContent has issues
        // with Stability AI's strict parser ("Content-Disposition missing a name").
        var boundary = $"StabilityAI{Guid.NewGuid():N}";
        byte[] crlf = [(byte)'\r', (byte)'\n'];
        using var body = new MemoryStream();

        void WriteTextField(string name, string value)
        {
            var header = $"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n";
            body.Write(Encoding.UTF8.GetBytes(header));
            body.Write(Encoding.UTF8.GetBytes(value));
            body.Write(crlf);
        }

        WriteTextField("prompt", prompt);
        WriteTextField("mode", "image-to-image");
        WriteTextField("model", _stabilityModel);
        WriteTextField("strength", _stabilityStrength.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        WriteTextField("negative_prompt", "ugly, blurry, low quality, deformed, disfigured, extra limbs, changed hair, different person, different clothes, different background, style change, color change");
        WriteTextField("output_format", "jpeg");

        // Image part
        var imageHeader = $"--{boundary}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"face.jpg\"\r\nContent-Type: image/jpeg\r\n\r\n";
        body.Write(Encoding.UTF8.GetBytes(imageHeader));
        body.Write(imageBytes);
        body.Write(crlf);
        body.Write(Encoding.UTF8.GetBytes($"--{boundary}--\r\n"));

        body.Position = 0;
        using var content = new StreamContent(body);
        content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v2beta/stable-image/generate/sd3") { Content = content };
        using var response = await _stabilityHttp!.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            var msg = $"Stability AI returned {(int)response.StatusCode} {response.ReasonPhrase}: {err}";
            _logger?.LogWarning("{Message}", msg);
            if (_logger == null) Console.Error.WriteLine($"[AvatarVideoGenerator] {msg}");
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("image", out var imageEl))
        {
            var finishReason = doc.RootElement.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;
            if (finishReason == "CONTENT_FILTERED")
            {
                _logger?.LogWarning("Stability AI filtered the generated image");
                return null;
            }

            return imageEl.GetString();
        }

        return null;
    }

    /// <summary>
    /// Sends a face crop to Forge SD img2img. Returns null on any failure (→ CSS fallback).
    /// </summary>
    private async Task<string?> CallSdFaceAsync(string prompt, string faceSeedBase64, int width, int height, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["prompt"] = prompt,
            ["negative_prompt"] = "ugly, blurry, low quality, deformed, disfigured, extra limbs, changed hair, different person, different clothes, different background, style change, color change",
            ["init_images"] = new[] { faceSeedBase64 },
            ["denoising_strength"] = 0.22,
            ["steps"] = 8,
            ["cfg_scale"] = 5,
            ["width"] = width,
            ["height"] = height,
            ["sampler_name"] = "Euler",
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("/sdapi/v1/img2img", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            var msg = $"Forge SD returned {(int)response.StatusCode} {response.ReasonPhrase}: {err}";
            _logger?.LogWarning("{Message}", msg);
            if (_logger == null) Console.Error.WriteLine($"[AvatarVideoGenerator] {msg}");
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("images", out var imgs)
            && imgs.ValueKind == JsonValueKind.Array
            && imgs.GetArrayLength() > 0)
        {
            return imgs[0].GetString();
        }

        return null;
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
