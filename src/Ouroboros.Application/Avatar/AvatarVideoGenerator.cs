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
public sealed partial class AvatarVideoGenerator
{
    private readonly HttpClient _http;
    private readonly HttpClient? _stabilityHttp;
    private readonly string? _sdCheckpoint;
    private readonly string? _stabilityAiApiKey;
    private readonly string _stabilityModel;
    private readonly double _stabilityStrength;
    private readonly string? _assetDirectory;
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
        string sdEndpoint = Configuration.DefaultEndpoints.StableDiffusion,
        string? sdModel = null,
        int timeoutSeconds = 300,
        ILogger<AvatarVideoGenerator>? logger = null,
        IVisionModel? visionModel = null,
        string? stabilityAiApiKey = null,
        string stabilityModel = "sd3.5-large-turbo",
        double stabilityStrength = 0.15,
        string? assetDirectory = null)
    {
        _sdCheckpoint = string.IsNullOrWhiteSpace(sdModel) || sdModel == "stable-diffusion"
            ? null
            : sdModel;
        _logger = logger;
        _visionModel = visionModel;
        _stabilityAiApiKey = stabilityAiApiKey;
        _stabilityModel = stabilityModel;
        _stabilityStrength = stabilityStrength;
        _assetDirectory = assetDirectory;
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
    /// <param name="prompt">Expression prompt for the generation.</param>
    /// <param name="seedBase64">Base64-encoded seed image (JPEG or PNG).</param>
    /// <param name="targetState">Current visual state — used to build reference sheet with all expressions.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<string?> GenerateFrameAsync(string prompt, string seedBase64, AvatarVisualState targetState, CancellationToken ct)
    {
        try
        {
            // ── 1. Encode seed as JPEG ────────────────────────────────────────────
            byte[] seedBytes = Convert.FromBase64String(seedBase64);
            string seedJpegBase64;

            // Convert to 24bpp JPEG if not already JPEG
            if (seedBytes.Length >= 2 && seedBytes[0] == 0xFF && seedBytes[1] == 0xD8)
            {
                seedJpegBase64 = seedBase64;
            }
            else
            {
                using var ms = new MemoryStream(seedBytes);
                using var bmp = new Bitmap(ms);
                using var rgb = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(rgb))
                    g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
                using var jpegMs = new MemoryStream();
                rgb.Save(jpegMs, ImageFormat.Jpeg);
                seedJpegBase64 = Convert.ToBase64String(jpegMs.ToArray());
            }

            // ── 2. Cloud path: build reference sheet with all expressions ─────────
            if (_stabilityHttp != null)
            {
                // Build a composite reference sheet so the model sees all expressions
                // of the same character, anchoring identity across the sheet.
                var (compositeJpeg, cropRect) = BuildReferenceSheet(seedBytes, targetState);
                string compositeBase64 = Convert.ToBase64String(compositeJpeg);

                var sheetPrompt = "character expression reference sheet, identical character in each cell, " +
                    "adult woman, same face shape, same sharp features, same outfit, same art style, " +
                    "consistent identity across all cells, subtle expression variation only: " + prompt;

                string? resultBase64 = await CallStabilityAiFaceAsync(sheetPrompt, compositeBase64, ct);
                if (resultBase64 == null) return null;

                // Crop the target expression cell from the generated sheet
                return CropCellFromSheet(resultBase64, cropRect);
            }

            // ── 3. Local SD path: face crop + composite (unchanged) ───────────────
            using var seedMs2 = new MemoryStream(seedBytes);
            using var seedBmp = new Bitmap(seedMs2);
            int faceH = (int)(seedBmp.Height * 0.48) / 8 * 8;
            var faceRect = new Rectangle(0, 0, seedBmp.Width, faceH);
            using var faceBmp = (Bitmap)seedBmp.Clone(faceRect, seedBmp.PixelFormat);

            string faceSeedBase64;
            using (var rgb = new Bitmap(faceBmp.Width, faceBmp.Height, PixelFormat.Format24bppRgb))
            {
                using (var gRgb = Graphics.FromImage(rgb))
                    gRgb.DrawImage(faceBmp, 0, 0, faceBmp.Width, faceBmp.Height);
                using var faceMs = new MemoryStream();
                rgb.Save(faceMs, ImageFormat.Jpeg);
                faceSeedBase64 = Convert.ToBase64String(faceMs.ToArray());
            }

            string? faceResultBase64 = await CallSdFaceAsync(prompt, faceSeedBase64, seedBmp.Width, faceH, ct);
            if (faceResultBase64 == null) return null;

            // Gatekeeper + corrector (local SD only)
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
                    var fix = await _visionModel.AnswerQuestionAsync(
                        faceResultBytes, "jpeg",
                        $"Target expression: {prompt}. What does this face actually show and what 1-line expression prompt would correct it? Output only the corrected prompt.",
                        ct);

                    if (fix.IsSuccess)
                    {
                        faceResultBase64 = await CallSdFaceAsync(fix.Value.Trim(), faceSeedBase64, seedBmp.Width, faceH, ct);
                        if (faceResultBase64 == null) return null;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            // Composite face onto body
            byte[] resultBytes = Convert.FromBase64String(faceResultBase64);
            using var faceResultMs = new MemoryStream(resultBytes);
            using var faceResultBmp = new Bitmap(faceResultMs);

            int blendZoneH = faceH * 22 / 100;
            int solidH = faceH - blendZoneH;
            const int BlendSteps = 16;
            int stripH = Math.Max(1, blendZoneH / BlendSteps);

            using var composite = new Bitmap(seedBmp.Width, seedBmp.Height, PixelFormat.Format32bppArgb);
            using var g2 = Graphics.FromImage(composite);
            g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g2.SmoothingMode = SmoothingMode.HighQuality;
            g2.DrawImage(seedBmp, 0, 0, seedBmp.Width, seedBmp.Height);
            g2.DrawImage(faceResultBmp,
                new Rectangle(0, 0, seedBmp.Width, solidH),
                new RectangleF(0, 0, faceResultBmp.Width, (float)solidH / faceH * faceResultBmp.Height),
                GraphicsUnit.Pixel);

            using var ia = new ImageAttributes();
            for (int i = 0; i < BlendSteps; i++)
            {
                float alpha = 1.0f - (float)(i + 1) / BlendSteps;
                int dstY = solidH + i * stripH;
                int dstH = i == BlendSteps - 1 ? faceH - dstY : stripH;
                if (dstH <= 0) continue;
                ia.SetColorMatrix(new ColorMatrix { Matrix33 = alpha });
                g2.DrawImage(faceResultBmp,
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

}
