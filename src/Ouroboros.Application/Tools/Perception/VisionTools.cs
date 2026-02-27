// <copyright file="VisionTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Text;
using Ouroboros.Application.Services;
using Ouroboros.Core.Monads;

/// <summary>
/// Vision AI tools: SeeScreen, DescribeImage, ReadTextFromScreen, WhatAmIDoing, DetectObjects.
/// </summary>
public static partial class PerceptionTools
{
    /// <summary>
    /// Look at the screen and understand what's visible using AI vision.
    /// </summary>
    public class SeeScreenTool : ITool
    {
        public string Name => "see_screen";
        public string Description => "Look at my screen using AI vision and describe what I see. I can understand the content, applications, and context. Input (optional): specific question about what's on screen.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (VisionService == null)
            {
                return Result<string, string>.Failure("Vision service not available. Make sure a vision model (llava) is running.");
            }

            try
            {
                var prompt = string.IsNullOrWhiteSpace(input)
                    ? null
                    : input.Trim();

                var result = await VisionService.CaptureAndAnalyzeScreenAsync(prompt, ct: ct);

                if (!result.Success)
                {
                    return Result<string, string>.Failure(result.ErrorMessage ?? "Vision analysis failed");
                }

                var sb = new StringBuilder();
                sb.AppendLine("👁️ **What I See on Screen:**\n");
                sb.AppendLine(result.Description);
                sb.AppendLine($"\n_Analyzed at {result.Timestamp:HH:mm:ss} using {result.Model}_");

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Vision failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Describe an image file using AI vision.
    /// </summary>
    public class DescribeImageTool : ITool
    {
        public string Name => "describe_image";
        public string Description => "Use AI vision to describe an image file in detail. Input: path to image file, optionally with a question (e.g., 'screenshot.png what color is the button?').";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (VisionService == null)
            {
                return Result<string, string>.Failure("Vision service not available.");
            }

            var parts = input.Trim().Split(' ', 2);
            var imagePath = parts[0].Trim('"');
            var prompt = parts.Length > 1 ? parts[1] : null;

            // Try to resolve relative path
            if (!Path.IsPathRooted(imagePath))
            {
                var capturesPath = Path.Combine(CaptureDirectory, imagePath);
                if (File.Exists(capturesPath))
                {
                    imagePath = capturesPath;
                }
                else
                {
                    imagePath = Path.Combine(Environment.CurrentDirectory, imagePath);
                }
            }

            try
            {
                var result = await VisionService.AnalyzeImageAsync(imagePath, prompt, ct);

                if (!result.Success)
                {
                    return Result<string, string>.Failure(result.ErrorMessage ?? "Image analysis failed");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"🖼️ **Image Analysis: {Path.GetFileName(imagePath)}**\n");
                sb.AppendLine(result.Description);

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Image analysis failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Read text (OCR) from the screen using AI vision.
    /// </summary>
    public class ReadTextFromScreenTool : ITool
    {
        public string Name => "read_screen_text";
        public string Description => "Read and transcribe all visible text from the screen using AI vision (OCR). Useful for understanding dialogs, error messages, or document content.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (VisionService == null)
            {
                return Result<string, string>.Failure("Vision service not available.");
            }

            try
            {
                var prompt = "Read and transcribe ALL visible text on this screen. Include text from windows, dialogs, buttons, menus, documents, code editors, and any other visible text. Format it clearly.";

                var result = await VisionService.CaptureAndAnalyzeScreenAsync(prompt, ct: ct);

                if (!result.Success)
                {
                    return Result<string, string>.Failure(result.ErrorMessage ?? "OCR failed");
                }

                var sb = new StringBuilder();
                sb.AppendLine("📖 **Text Visible on Screen:**\n");
                sb.AppendLine(result.Description);

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"OCR failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Understand what the user is currently doing.
    /// </summary>
    public class WhatAmIDoingTool : ITool
    {
        public string Name => "what_am_i_doing";
        public string Description => "Use AI vision to understand and describe what I (the user) am currently doing on my computer. Provides context about current task and activity.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (VisionService == null)
            {
                return Result<string, string>.Failure("Vision service not available.");
            }

            try
            {
                var result = await VisionService.DescribeUserActivityAsync(ct);

                if (!result.Success)
                {
                    return Result<string, string>.Failure(result.ErrorMessage ?? "Activity analysis failed");
                }

                var sb = new StringBuilder();
                sb.AppendLine("🔍 **Current User Activity Analysis:**\n");
                sb.AppendLine(result.Description);

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Activity analysis failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect specific objects or elements on screen.
    /// </summary>
    public class DetectObjectsTool : ITool
    {
        public string Name => "detect_on_screen";
        public string Description => "Detect specific objects, elements, or conditions on screen. Input: what to look for (e.g., 'error dialog', 'red button', 'loading spinner', 'specific text').";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (VisionService == null)
            {
                return Result<string, string>.Failure("Vision service not available.");
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                return Result<string, string>.Failure("Please specify what to detect (e.g., 'error dialog', 'submit button').");
            }

            try
            {
                var prompt = $"Look at this screen and answer: Can you see '{input.Trim()}'? If yes, describe where it is and its current state. If no, describe what you see instead.";

                var result = await VisionService.CaptureAndAnalyzeScreenAsync(prompt, ct: ct);

                if (!result.Success)
                {
                    return Result<string, string>.Failure(result.ErrorMessage ?? "Detection failed");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"🎯 **Detection: '{input.Trim()}'**\n");
                sb.AppendLine(result.Description);

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Detection failed: {ex.Message}");
            }
        }
    }
}
