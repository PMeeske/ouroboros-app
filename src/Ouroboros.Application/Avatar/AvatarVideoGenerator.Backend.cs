// <copyright file="AvatarVideoGenerator.Backend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Application.Avatar;

/// <summary>
/// Backend methods: prompt building, reference sheet composition, cloud/local SD API calls, and asset path resolution.
/// </summary>
public sealed partial class AvatarVideoGenerator
{
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
    /// Builds a 2x3 reference sheet compositing all 5 expression assets plus the target.
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

        // Layout: 2 rows x 3 columns
        // [0,0]=other[0]  [0,1]=TARGET  [0,2]=other[1]
        // [1,0]=other[2]  [1,1]=other[3] [1,2]=TARGET (duplicate for emphasis)
        const int Cols = 3, Rows = 2;

        // Load the target image
        using var targetMs = new MemoryStream(targetImageBytes);
        using var targetBmp = new Bitmap(targetMs);
        int cellW = targetBmp.Width;
        int cellH = targetBmp.Height;

        // Scale cells so total sheet fits within 1536x1536 (SD3 limit)
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
    /// Returns base64-encoded JPEG on success, null on failure (-> CSS fallback).
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
    /// Sends a face crop to Forge SD img2img. Returns null on any failure (-> CSS fallback).
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
