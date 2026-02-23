// <copyright file="AvatarAssetPreparer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Drawing;
using System.Drawing.Imaging;
using Spectre.Console;

namespace Ouroboros.CLI.Avatar;

/// <summary>
/// Crops character reference sheets into individual avatar image assets.
/// Used to extract holographic wireframe overlays from a composite grid image.
/// </summary>
public static class AvatarAssetPreparer
{
    /// <summary>Maps a grid cell to an output filename.</summary>
    /// <param name="Row">Zero-based row index.</param>
    /// <param name="Column">Zero-based column index.</param>
    /// <param name="OutputFilename">Target filename (e.g. "holo_front.png").</param>
    public record CellMapping(int Row, int Column, string OutputFilename);

    /// <summary>
    /// Default mapping for the Iaret character sheet.
    /// Sheet layout: 1024×1536, 3 rows × 6 effective columns.
    /// Left 3 columns = solid renders, right 3 columns = wireframe renders.
    /// Rows: 0 = full body, 1 = torso, 2 = head/portrait.
    /// Wireframe columns: 3 = front, 4 = three-quarter, 5 = side.
    /// </summary>
    public static readonly IReadOnlyList<CellMapping> IaretHoloMappings =
    [
        // Full-body wireframes (top row, right half)
        new(0, 3, "holo_front.png"),
        new(0, 4, "holo_threequarter.png"),
        new(0, 5, "holo_side.png"),

        // Torso wireframes (middle row) — back and side-left views
        new(1, 3, "holo_back.png"),
        new(1, 4, "holo_sideleft.png"),

        // Head wireframe (bottom row) — portrait overlay
        new(2, 3, "holo_portrait.png"),
    ];

    /// <summary>
    /// Crops a character sheet image into individual cell images.
    /// Divides the sheet into a uniform grid and extracts specified cells.
    /// </summary>
    /// <param name="sheetPath">Path to the character sheet image.</param>
    /// <param name="outputDirectory">Directory to write cropped images.</param>
    /// <param name="rows">Number of rows in the grid.</param>
    /// <param name="columns">Number of columns in the grid.</param>
    /// <param name="mappings">Which cells to extract and their target filenames.</param>
    /// <param name="gutterX">Horizontal gutter/padding to trim from each cell edge (pixels).</param>
    /// <param name="gutterY">Vertical gutter/padding to trim from each cell edge (pixels).</param>
    /// <returns>List of output file paths that were created.</returns>
    public static IReadOnlyList<string> CropCharacterSheet(
        string sheetPath,
        string outputDirectory,
        int rows,
        int columns,
        IReadOnlyList<CellMapping> mappings,
        int gutterX = 0,
        int gutterY = 0)
    {
        using var sheet = new Bitmap(sheetPath);
        var cellWidth = sheet.Width / columns;
        var cellHeight = sheet.Height / rows;
        var created = new List<string>();

        foreach (var mapping in mappings)
        {
            if (mapping.Row >= rows || mapping.Column >= columns)
            {
                continue;
            }

            var x = mapping.Column * cellWidth + gutterX;
            var y = mapping.Row * cellHeight + gutterY;
            var w = cellWidth - (gutterX * 2);
            var h = cellHeight - (gutterY * 2);

            // Clamp to image bounds
            x = Math.Max(0, Math.Min(x, sheet.Width - 1));
            y = Math.Max(0, Math.Min(y, sheet.Height - 1));
            w = Math.Min(w, sheet.Width - x);
            h = Math.Min(h, sheet.Height - y);

            var rect = new Rectangle(x, y, w, h);
            using var cell = sheet.Clone(rect, sheet.PixelFormat);

            var outputPath = Path.Combine(outputDirectory, mapping.OutputFilename);
            cell.Save(outputPath, ImageFormat.Png);
            created.Add(outputPath);
        }

        return created;
    }

    /// <summary>
    /// Auto-detects grid dimensions by scanning for dark gutters between cells.
    /// Useful when the exact grid layout is unknown.
    /// </summary>
    /// <param name="sheetPath">Path to the character sheet image.</param>
    /// <param name="axis">Which axis to scan: "horizontal" for column boundaries, "vertical" for row boundaries.</param>
    /// <param name="darkThreshold">Maximum brightness (0–255) to consider a pixel "dark".</param>
    /// <param name="gutterMinWidth">Minimum width of a dark strip to count as a gutter.</param>
    /// <returns>List of detected boundary positions (pixel offsets).</returns>
    public static IReadOnlyList<int> DetectGridBoundaries(
        string sheetPath,
        string axis = "horizontal",
        int darkThreshold = 30,
        int gutterMinWidth = 3)
    {
        using var sheet = new Bitmap(sheetPath);
        var boundaries = new List<int>();
        var scanLength = axis == "horizontal" ? sheet.Width : sheet.Height;
        var crossLength = axis == "horizontal" ? sheet.Height : sheet.Width;

        var consecutiveDark = 0;

        for (var i = 0; i < scanLength; i++)
        {
            // Average brightness across the cross-axis for this strip
            long totalBrightness = 0;
            var sampleStep = Math.Max(1, crossLength / 50); // Sample ~50 pixels for speed

            for (var j = 0; j < crossLength; j += sampleStep)
            {
                var pixel = axis == "horizontal"
                    ? sheet.GetPixel(i, j)
                    : sheet.GetPixel(j, i);

                totalBrightness += (pixel.R + pixel.G + pixel.B) / 3;
            }

            var avgBrightness = totalBrightness / (crossLength / sampleStep);

            if (avgBrightness <= darkThreshold)
            {
                consecutiveDark++;
            }
            else
            {
                if (consecutiveDark >= gutterMinWidth)
                {
                    // Record the midpoint of the dark strip
                    boundaries.Add(i - (consecutiveDark / 2));
                }

                consecutiveDark = 0;
            }
        }

        // Check trailing dark strip
        if (consecutiveDark >= gutterMinWidth)
        {
            boundaries.Add(scanLength - (consecutiveDark / 2));
        }

        return boundaries;
    }

    /// <summary>
    /// Prepares holographic wireframe assets for the Iaret avatar.
    /// Crops the character sheet if present and holo images are missing.
    /// </summary>
    /// <param name="assetDirectory">The Iaret asset directory containing the character sheet.</param>
    /// <returns>True if assets were prepared, false if already present or no sheet found.</returns>
    public static bool PrepareIaretHolographics(string assetDirectory)
    {
        if (AllHolographicsExist(assetDirectory))
        {
            return false;
        }

        var sheetPath = FindCharacterSheet(assetDirectory);
        if (sheetPath == null)
        {
            return false;
        }

        AnsiConsole.MarkupLine($"[rgb(148,103,189)]  [Avatar] Cropping character sheet for holographic assets...[/]");

        // Iaret sheet: 1024×1536 → 6 columns × 3 rows
        var created = CropCharacterSheet(
            sheetPath,
            assetDirectory,
            rows: 3,
            columns: 6,
            IaretHoloMappings);

        AnsiConsole.MarkupLine($"[rgb(148,103,189)]  [Avatar] Created {created.Count} holographic overlay(s)[/]");

        return created.Count > 0;
    }

    private static bool AllHolographicsExist(string directory)
    {
        return IaretHoloMappings.All(m =>
            File.Exists(Path.Combine(directory, m.OutputFilename)));
    }

    private static string? FindCharacterSheet(string directory)
    {
        // Look for the known Iaret character sheet by filename pattern
        var knownSheet = Path.Combine(directory, "file_000000008eb4720aada3379894008da5.png");
        if (File.Exists(knownSheet))
        {
            return knownSheet;
        }

        // Fallback: look for any file named *sheet* or *charsheet*
        var candidates = Directory.GetFiles(directory, "*.png")
            .Where(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                return name.Contains("sheet") || name.Contains("charsheet") || name.Contains("reference");
            })
            .ToArray();

        return candidates.Length == 1 ? candidates[0] : null;
    }
}
