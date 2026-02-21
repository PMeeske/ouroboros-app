// <copyright file="AlgorithmicExpressionGenerator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Drawing;
using System.Drawing.Imaging;

namespace Ouroboros.Application.Avatar;

/// <summary>
/// Generates avatar expression frames algorithmically using GDI+ image region transforms.
/// No ML model or external service required — pure pixel manipulation.
///
/// Approach:
/// <list type="number">
///   <item>Apply a per-state <see cref="ColorMatrix"/> to the face region (brightness + warmth/coolness).</item>
///   <item>Shift the brow strip up or down by a few pixels to simulate raised/furrowed brows.</item>
///   <item>Apply a secondary brightness adjustment to the mouth region (simulate open/closed).</item>
///   <item>Composite the result back onto the full seed image and encode as JPEG.</item>
/// </list>
/// </summary>
public static class AlgorithmicExpressionGenerator
{
    private sealed record ExpressionParams(
        float Brightness,       // 1.0 = unchanged; >1.0 = brighter face
        float Warmth,           // positive = warmer (R+, B-); negative = cooler (R-, B+)
        int BrowOffsetY,        // pixels: negative = raise brows, positive = lower brows
        float MouthBrightness); // additional brightness multiplier for mouth region

    // Baseline parameters per visual state.
    // Keep values subtle — these overlay real portrait photos, so large deltas look wrong.
    private static readonly Dictionary<AvatarVisualState, ExpressionParams> BaseParams = new()
    {
        [AvatarVisualState.Idle]        = new(1.00f,  0.00f,  0, 1.00f),
        [AvatarVisualState.Listening]   = new(1.02f,  0.01f, -3, 1.00f),
        [AvatarVisualState.Thinking]    = new(0.97f, -0.02f, -2, 0.97f),
        [AvatarVisualState.Speaking]    = new(1.03f,  0.02f,  0, 1.07f),
        [AvatarVisualState.Encouraging] = new(1.05f,  0.04f, -4, 1.04f),
    };

    // Mood deltas layered on top of the state baseline.
    private static readonly Dictionary<string, (float Brightness, float Warmth)> MoodDeltas = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["warm"]         = ( 0.02f,  0.02f),
        ["happy"]        = ( 0.02f,  0.02f),
        ["excited"]      = ( 0.04f,  0.01f),
        ["enthusiastic"] = ( 0.04f,  0.01f),
        ["calm"]         = (-0.01f,  0.00f),
        ["serene"]       = (-0.01f,  0.00f),
        ["sad"]          = (-0.03f, -0.03f),
        ["melancholic"]  = (-0.03f, -0.03f),
        ["resolute"]     = ( 0.00f,  0.01f),
        ["determined"]   = ( 0.00f,  0.01f),
        ["curious"]      = ( 0.01f,  0.00f),
        ["concerned"]    = (-0.01f, -0.01f),
        ["worried"]      = (-0.01f, -0.01f),
    };

    /// <summary>
    /// Applies an algorithmic expression to the seed image and returns JPEG bytes.
    /// </summary>
    /// <param name="seedBytes">Raw bytes of the seed PNG/JPEG portrait.</param>
    /// <param name="state">Current avatar state driving the expression.</param>
    /// <returns>JPEG-encoded bytes of the modified image.</returns>
    public static byte[] ApplyExpression(byte[] seedBytes, AvatarStateSnapshot state)
    {
        var p = ResolveParams(state);

        using var ms = new MemoryStream(seedBytes);
        using var src = new Bitmap(ms);

        int w = src.Width;
        int h = src.Height;

        // Face region: top 48 % (consistent with SD crop convention).
        int faceH = (int)(h * 0.48);

        using var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result);

        // ── 1. Base: draw the full seed ────────────────────────────────────────
        g.DrawImage(src, 0, 0, w, h);

        // ── 2. Face colour adjustment (brightness + warmth) ────────────────────
        bool doColour = MathF.Abs(p.Brightness - 1f) > 0.001f || MathF.Abs(p.Warmth) > 0.001f;
        if (doColour)
            DrawWithMatrix(g, src, new Rectangle(0, 0, w, faceH), BuildMatrix(p.Brightness, p.Warmth));

        // ── 3. Brow offset ─────────────────────────────────────────────────────
        if (p.BrowOffsetY != 0)
            ApplyBrowOffset(g, src, w, faceH, p.BrowOffsetY);

        // ── 4. Mouth region brightness ─────────────────────────────────────────
        if (MathF.Abs(p.MouthBrightness - 1f) > 0.001f)
        {
            int mouthTop = (int)(faceH * 0.62);
            int mouthH   = (int)(faceH * 0.20);
            DrawWithMatrix(g, src, new Rectangle(0, mouthTop, w, mouthH), BuildMatrix(p.MouthBrightness, 0));
        }

        // ── 5. Encode ─────────────────────────────────────────────────────────
        using var outMs = new MemoryStream();
        result.Save(outMs, ImageFormat.Jpeg);
        return outMs.ToArray();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ExpressionParams ResolveParams(AvatarStateSnapshot state)
    {
        var p = BaseParams.GetValueOrDefault(state.VisualState, BaseParams[AvatarVisualState.Idle]);

        if (state.Mood is { Length: > 0 } mood &&
            MoodDeltas.TryGetValue(mood, out var delta))
        {
            p = p with
            {
                Brightness = p.Brightness + delta.Brightness,
                Warmth     = p.Warmth     + delta.Warmth,
            };
        }

        return p;
    }

    /// <summary>
    /// Builds a 5×5 GDI+ <see cref="ColorMatrix"/> that scales RGB channels
    /// for <paramref name="brightness"/> and shifts red/blue for <paramref name="warmth"/>.
    /// </summary>
    private static ColorMatrix BuildMatrix(float brightness, float warmth) => new(
    [
        [brightness + warmth * 2f, 0f, 0f, 0f, 0f], // R row
        [0f, brightness,           0f, 0f, 0f],       // G row
        [0f, 0f, brightness - warmth, 0f, 0f],        // B row
        [0f, 0f, 0f,               1f, 0f],           // A row (unchanged)
        [0f, 0f, 0f,               0f, 1f],           // translation (none)
    ]);

    /// <summary>
    /// Draws a sub-region of <paramref name="src"/> onto <paramref name="g"/>
    /// with a <see cref="ColorMatrix"/> applied.
    /// </summary>
    private static void DrawWithMatrix(Graphics g, Bitmap src, Rectangle region, ColorMatrix matrix)
    {
        using var ia = new ImageAttributes();
        ia.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        g.DrawImage(src, region,
            region.X, region.Y, region.Width, region.Height,
            GraphicsUnit.Pixel, ia);
    }

    /// <summary>
    /// Shifts the brow strip vertically by <paramref name="offsetY"/> pixels.
    /// Negative = raise (brows move up), positive = lower.
    /// The vacated strip is filled by edge-extending the adjacent row.
    /// </summary>
    private static void ApplyBrowOffset(Graphics g, Bitmap src, int w, int faceH, int offsetY)
    {
        // Brow band: roughly 18–32 % of the face crop.
        int browTop = (int)(faceH * 0.18);
        int browH   = (int)(faceH * 0.14);
        int browBot = browTop + browH;
        int clampedOffset = Math.Clamp(offsetY, -browTop, faceH - browBot);
        if (clampedOffset == 0) return;

        // Extract brow strip from original.
        using var browBmp = new Bitmap(w, browH, PixelFormat.Format32bppArgb);
        using (var bg = Graphics.FromImage(browBmp))
            bg.DrawImage(src,
                new Rectangle(0, 0, w, browH),
                new Rectangle(0, browTop, w, browH),
                GraphicsUnit.Pixel);

        int fillSize = Math.Abs(clampedOffset);

        if (clampedOffset < 0)
        {
            // Raising brows: fill the gap that appears at browBot+offset by repeating the
            // row just below the original brow (natural skin/hair continuation).
            int fillY    = Math.Min(browBot, src.Height - fillSize);
            int fillDstY = browBot + clampedOffset;
            g.DrawImage(src,
                new Rectangle(0, fillDstY, w, fillSize),
                new Rectangle(0, fillY, w, fillSize),
                GraphicsUnit.Pixel);
        }
        else
        {
            // Lowering brows: fill the gap at browTop by repeating the row just above.
            int fillY    = Math.Max(0, browTop - fillSize);
            g.DrawImage(src,
                new Rectangle(0, browTop, w, fillSize),
                new Rectangle(0, fillY, w, fillSize),
                GraphicsUnit.Pixel);
        }

        // Draw the brow strip at its new position.
        g.DrawImage(browBmp,
            new Rectangle(0, browTop + clampedOffset, w, browH),
            new Rectangle(0, 0, w, browH),
            GraphicsUnit.Pixel);
    }
}
