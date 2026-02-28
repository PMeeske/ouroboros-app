// <copyright file="AnalyzeImageTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Drawing;
using System.Text;
using Ouroboros.Core.Monads;

public static partial class PerceptionTools
{
    /// <summary>
    /// Analyze an image using vision model.
    /// </summary>
    public class AnalyzeImageTool : ITool
    {
        public string Name => "analyze_image";
        public string Description => "Analyze an image using vision AI. Input: path to image file. Describes what's visible in the image.";
        public string? JsonSchema => null;

        /// <summary>
        /// Vision model endpoint for image analysis.
        /// </summary>
        public static Func<string, CancellationToken, Task<string>>? VisionAnalyzer { get; set; }

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                var imagePath = input.Trim().Trim('"');
                if (!Path.IsPathRooted(imagePath))
                {
                    imagePath = Path.Combine(CaptureDirectory, imagePath);
                }

                if (!File.Exists(imagePath))
                {
                    return Result<string, string>.Failure($"Image not found: {imagePath}");
                }

                if (VisionAnalyzer != null)
                {
                    var analysis = await VisionAnalyzer(imagePath, ct);
                    return Result<string, string>.Success($"🔍 **Image Analysis**\n\n{analysis}");
                }

                // Fallback: basic image info
                using var img = Image.FromFile(imagePath);
                var info = new StringBuilder();
                info.AppendLine("📊 **Image Information**\n");
                info.AppendLine($"**File:** {Path.GetFileName(imagePath)}");
                info.AppendLine($"**Size:** {img.Width}x{img.Height}");
                info.AppendLine($"**Format:** {img.RawFormat}");
                info.AppendLine($"**File size:** {new FileInfo(imagePath).Length / 1024} KB");
                info.AppendLine("\n_Note: Vision analysis requires a vision model to be configured._");

                return Result<string, string>.Success(info.ToString());
            }
            catch (IOException ex)
            {
                return Result<string, string>.Failure($"Image analysis failed: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                return Result<string, string>.Failure($"Image analysis failed: {ex.Message}");
            }
        }
    }
}
