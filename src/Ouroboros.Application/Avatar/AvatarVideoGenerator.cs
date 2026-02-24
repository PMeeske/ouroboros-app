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
        string sdEndpoint = "http://localhost:7860",
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
    /// Builds an emotional prompt from the current avatar state snapshot.
    /// The prompt is tailored to Iaret's visual state and mood to guide SD generation.
    /// </summary>
    public static string BuildPrompt(AvatarStateSnapshot state)
    {
        // Facial expression per visual state — these are the primary emotional drivers.
        var expressionPrompt = state.VisualState switch
        {
            AvatarVisualState.Idle =>
                "calm composed expression, relaxed face, gentle steady gaze, eyes forward",
            AvatarVisualState.Listening =>
                "attentive expression, focused gaze, raised brows, head tilted slightly, open receptive face",
            AvatarVisualState.Thinking =>
                "deep contemplative expression, eyes looking upward, furrowed brow, thoughtful introspective face",
            AvatarVisualState.Speaking =>
                "expressive speaking face, open mouth, animated lively eyes, engaged direct gaze, dynamic expression",
            AvatarVisualState.Encouraging =>
                "warm bright smile, kind glowing eyes, encouraging joyful expression, radiant face",
            _ =>
                "neutral expression",
        };

        // Mood modifier — stronger, more specific emotional cues
        var moodModifier = state.Mood?.ToLowerInvariant() switch
        {
            "warm" or "happy" or "joyful" => ", genuinely happy eyes, warm smile, glowing",
            "resolute" or "determined" => ", firm set jaw, intense confident gaze, strong expression",
            "curious" or "intrigued" => ", raised eyebrow, widened curious eyes, fascinated look",
            "calm" or "serene" or "peaceful" => ", deeply peaceful face, half-closed serene eyes, tranquil",
            "concerned" or "worried" => ", worried frown, creased brow, empathetic pained eyes",
            "excited" or "enthusiastic" or "thrilled" => ", bright wide sparkling eyes, beaming excited face, high energy",
            "sad" or "melancholic" or "wistful" => ", glistening sad eyes, downward gaze, sorrowful expression",
            "protective" or "vigilant" => ", piercing watchful gaze, set jaw, fierce protective expression",
            "loving" or "affectionate" or "tender" => ", soft adoring eyes, tender warm smile, loving gaze",
            "frustrated" or "intense" => ", tense jaw, narrowed eyes, intense frustrated expression",
            "surprised" or "amazed" => ", wide open eyes, raised brows, open mouth, shocked amazed face",
            "playful" or "mischievous" => ", sly smirk, sparkling mischievous eyes, playful grin",
            "proud" or "satisfied" => ", chin slightly raised, confident proud smile, accomplished look",
            "contemplative" or "philosophical" => ", distant unfocused gaze, deep in thought, profound expression",
            _ => string.Empty,
        };

        // Anchor: preserve identity, allow expressive face changes
        return "identical character, adult woman, same face shape, same sharp features, same outfit, same background, same art style, expressive face: " +
               expressionPrompt + moodModifier;
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

    /// <summary>
    /// Builds a 2×3 reference sheet compositing all 5 expression assets plus the target.
    /// The target expression is placed at the top-center cell (row 0, col 1).
    /// Other cells contain the 4 remaining static expression assets + a duplicate of the target.
    /// This gives the SD model full character identity context across all expressions.
    /// </summary>
    private (byte[] compositeJpeg, Rectangle cropRect) BuildReferenceSheet(
        byte[] targetImageBytes, AvatarVisualState targetState)
    {
        // All 5 visual states
        var allStates = new[]
        {
            AvatarVisualState.Idle,
            AvatarVisualState.Listening,
            AvatarVisualState.Thinking,
            AvatarVisualState.Speaking,
            AvatarVisualState.Encouraging,
        };

        // Separate target from other states
        var otherStates = allStates.Where(s => s != targetState).ToArray();

        // Layout: 2 rows × 3 columns
        // [0,0]=other[0]  [0,1]=TARGET  [0,2]=other[1]
        // [1,0]=other[2]  [1,1]=other[3] [1,2]=TARGET (duplicate for emphasis)
        const int Cols = 3, Rows = 2;

        // Load the target image
        using var targetMs = new MemoryStream(targetImageBytes);
        using var targetBmp = new Bitmap(targetMs);
        int cellW = targetBmp.Width;
        int cellH = targetBmp.Height;

        // Scale cells so total sheet fits within 1536×1536 (SD3 limit)
        float scale = 1.0f;
        if (cellW * Cols > 1536) scale = Math.Min(scale, 1536f / (cellW * Cols));
        if (cellH * Rows > 1536) scale = Math.Min(scale, 1536f / (cellH * Rows));
        int sCellW = (int)(cellW * scale) / 8 * 8; // Round to multiple of 8 for SD
        int sCellH = (int)(cellH * scale) / 8 * 8;
        if (sCellW < 8) sCellW = 8;
        if (sCellH < 8) sCellH = 8;
        int sheetW = sCellW * Cols;
        int sheetH = sCellH * Rows;

        using var composite = new Bitmap(sheetW, sheetH, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(composite);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;

        // Helper: draw a bitmap into a grid cell
        void DrawCell(Bitmap src, int col, int row)
        {
            g.DrawImage(src,
                new Rectangle(col * sCellW, row * sCellH, sCellW, sCellH),
                new Rectangle(0, 0, src.Width, src.Height),
                GraphicsUnit.Pixel);
        }

        // Load reference assets for the 4 non-target states
        var refBitmaps = new List<Bitmap>();
        foreach (var state in otherStates)
        {
            var path = _assetDirectory != null
                ? GetSeedAssetPath(state, _assetDirectory)
                : null;

            if (path != null && File.Exists(path))
            {
                refBitmaps.Add(new Bitmap(path));
            }
            else
            {
                // Fallback: duplicate target
                var cloneMs = new MemoryStream(targetImageBytes);
                refBitmaps.Add(new Bitmap(cloneMs));
            }
        }

        // Draw the grid:
        // Row 0: other[0], TARGET, other[1]
        // Row 1: other[2], other[3], TARGET (duplicate)
        if (refBitmaps.Count >= 1) DrawCell(refBitmaps[0], 0, 0);
        DrawCell(targetBmp, 1, 0); // TARGET at top-center
        if (refBitmaps.Count >= 2) DrawCell(refBitmaps[1], 2, 0);
        if (refBitmaps.Count >= 3) DrawCell(refBitmaps[2], 0, 1);
        if (refBitmaps.Count >= 4) DrawCell(refBitmaps[3], 1, 1);
        DrawCell(targetBmp, 2, 1); // Duplicate target at bottom-right

        // Clean up reference bitmaps
        foreach (var bmp in refBitmaps) bmp.Dispose();

        // Encode composite as JPEG
        using var outMs = new MemoryStream();
        composite.Save(outMs, ImageFormat.Jpeg);

        // Crop rect for target: row 0, col 1
        var cropRect = new Rectangle(sCellW, 0, sCellW, sCellH);

        return (outMs.ToArray(), cropRect);
    }

    /// <summary>
    /// Crops a single cell from a generated reference sheet and returns it as base64 JPEG.
    /// </summary>
    private static string? CropCellFromSheet(string sheetBase64, Rectangle cropRect)
    {
        byte[] sheetBytes = Convert.FromBase64String(sheetBase64);
        using var ms = new MemoryStream(sheetBytes);
        using var sheetBmp = new Bitmap(ms);

        // The generated sheet should match seed dimensions, but derive cell size
        // from actual output just in case the API resized it.
        int genCellW = sheetBmp.Width / 3;  // 3 columns
        int genCellH = sheetBmp.Height / 2; // 2 rows
        // Target is at col=1, row=0
        var genCropRect = new Rectangle(genCellW, 0, genCellW, genCellH);

        // Clamp to image bounds
        if (genCropRect.Right > sheetBmp.Width) genCropRect.Width = sheetBmp.Width - genCropRect.X;
        if (genCropRect.Bottom > sheetBmp.Height) genCropRect.Height = sheetBmp.Height - genCropRect.Y;

        using var cropped = sheetBmp.Clone(genCropRect, sheetBmp.PixelFormat);
        using var outMs = new MemoryStream();
        cropped.Save(outMs, ImageFormat.Jpeg);
        return Convert.ToBase64String(outMs.ToArray());
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
        WriteTextField("negative_prompt", "ugly, blurry, low quality, deformed, disfigured, extra limbs, changed hair, different person, different clothes, different background, style change, color change, baby face, child, young, round face, soft features, aged, wrinkles");
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
            ["negative_prompt"] = "ugly, blurry, low quality, deformed, disfigured, extra limbs, changed hair, different person, different clothes, different background, style change, color change, baby face, child, young, round face, soft features, aged, wrinkles",
            ["init_images"] = new[] { faceSeedBase64 },
            ["denoising_strength"] = 0.35,
            ["steps"] = 10,
            ["cfg_scale"] = 6.5,
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
