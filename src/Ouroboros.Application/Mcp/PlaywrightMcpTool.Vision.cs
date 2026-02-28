// <copyright file="PlaywrightMcpTool.Vision.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Services;

namespace Ouroboros.Application.Mcp;

/// <summary>
/// Vision-related functionality: screenshot processing, element detection,
/// text extraction (OCR), action suggestion, and page validation.
/// </summary>
public partial class PlaywrightMcpTool
{
    private async Task<string> ProcessScreenshotAsync(string mcpResponse, CancellationToken ct)
    {
        // Extract base64 image data from the response
        string? base64Data = ExtractBase64ImageData(mcpResponse);
        if (string.IsNullOrEmpty(base64Data))
        {
            return $"Screenshot captured but could not extract image data. Response length: {mcpResponse.Length} chars";
        }

        // Decode and save the image
        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException)
        {
            return $"Screenshot captured but base64 decoding failed. Data length: {base64Data.Length} chars";
        }

        // Save to file with timestamp
        string filename = $"screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_screenshotDirectory, filename);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);
        _lastScreenshotPath = filePath;

        // Get basic metadata
        var metadata = AnalyzeScreenshot(imageBytes);

        // Use Ministral vision model to understand the screenshot content
        string visionDescription;
        try
        {
            var visionResult = await _visionService.AnalyzeImageAsync(
                filePath,
                "Describe what you see in this browser screenshot. What page is shown? What are the main elements, text content, and any notable UI components? Be concise but comprehensive.",
                ct);

            visionDescription = visionResult.Success
                ? visionResult.Description ?? "No description available"
                : $"Vision analysis unavailable: {visionResult.ErrorMessage}";
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            visionDescription = $"Vision analysis failed: {ex.Message}";
        }

        return $"Screenshot captured and analyzed:\n" +
               $"  Path: {filePath}\n" +
               $"  Dimensions: {metadata.Width}x{metadata.Height} pixels\n" +
               $"  Size: {imageBytes.Length:N0} bytes ({imageBytes.Length / 1024.0:F1} KB)\n" +
               $"  Format: {metadata.Format}\n" +
               $"  Model: {_visionModel}\n" +
               $"\nVision Analysis:\n{visionDescription}";
    }

    private static string? ExtractBase64ImageData(string response)
    {
        // Try various formats the MCP server might return

        // 1. Look for data:image URI format
        int dataUriStart = response.IndexOf("data:image/", StringComparison.OrdinalIgnoreCase);
        if (dataUriStart >= 0)
        {
            int base64Start = response.IndexOf(";base64,", dataUriStart, StringComparison.OrdinalIgnoreCase);
            if (base64Start >= 0)
            {
                base64Start += 8; // Skip ";base64,"
                // Find the end - either a quote, whitespace, or end of string
                int end = base64Start;
                while (end < response.Length && IsBase64Char(response[end]))
                {
                    end++;
                }
                return response[base64Start..end];
            }
        }

        // 2. Try to parse as JSON and look for image field
        try
        {
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("image", out var imageEl))
            {
                return imageEl.GetString();
            }
            if (doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                return dataEl.GetString();
            }
            if (doc.RootElement.TryGetProperty("screenshot", out var screenshotEl))
            {
                return screenshotEl.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON, continue
        }

        // 3. If the entire response looks like base64, use it
        string trimmed = response.Trim();
        if (trimmed.Length > 100 && trimmed.All(IsBase64Char))
        {
            return trimmed;
        }

        return null;
    }

    private static bool IsBase64Char(char c) =>
        char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=';

    private static ScreenshotAnalysis AnalyzeScreenshot(byte[] imageBytes)
    {
        // Detect image format and dimensions from header
        int width = 0, height = 0;
        string format = "unknown";

        if (imageBytes.Length >= 24)
        {
            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
            {
                format = "PNG";
                // Width at bytes 16-19, height at 20-23 (big-endian)
                width = (imageBytes[16] << 24) | (imageBytes[17] << 16) | (imageBytes[18] << 8) | imageBytes[19];
                height = (imageBytes[20] << 24) | (imageBytes[21] << 16) | (imageBytes[22] << 8) | imageBytes[23];
            }
            // JPEG signature: FF D8 FF
            else if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF)
            {
                format = "JPEG";
                // JPEG dimensions require parsing SOF markers - simplified for now
                (width, height) = ParseJpegDimensions(imageBytes);
            }
        }

        // Generate description based on dimensions
        string description;
        if (width > 0 && height > 0)
        {
            float aspectRatio = (float)width / height;
            string orientation = aspectRatio > 1.2 ? "landscape" : aspectRatio < 0.8 ? "portrait" : "square-ish";
            string sizeCategory = (width * height) switch
            {
                > 2_000_000 => "high-resolution",
                > 500_000 => "medium-resolution",
                _ => "low-resolution"
            };
            description = $"{sizeCategory} {orientation} image";
        }
        else
        {
            description = "Image captured (dimensions could not be determined)";
        }

        return new ScreenshotAnalysis(width, height, format, description);
    }

    private static (int Width, int Height) ParseJpegDimensions(byte[] jpegBytes)
    {
        try
        {
            for (int i = 0; i < jpegBytes.Length - 9; i++)
            {
                // Look for a SOF (Start Of Frame) marker (SOF0, SOF1, SOF2)
                if (jpegBytes[i] == 0xFF && (jpegBytes[i + 1] >= 0xC0 && jpegBytes[i + 1] <= 0xC2))
                {
                    int height = (jpegBytes[i + 5] << 8) | jpegBytes[i + 6];
                    int width = (jpegBytes[i + 7] << 8) | jpegBytes[i + 8];
                    return (width, height);
                }
            }
        }
        catch
        {
            // If parsing fails for any reason, return 0,0.
        }
        return (0, 0);
    }

    /// <summary>
    /// Gets the vision analysis for the last screenshot taken. Internal use for clean data transfer.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw vision analysis text, or an error message.</returns>
    internal async Task<Result<string, string>> GetVisionAnalysisForLastScreenshotAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(LastScreenshotPath))
        {
            return Result<string, string>.Failure("No screenshot has been taken yet.");
        }

        try
        {
            var visionResult = await _visionService.AnalyzeImageAsync(
                LastScreenshotPath,
                "Describe what you see in this browser screenshot. What page is shown? What are the main elements, text content, and any notable UI components? Be concise but comprehensive.",
                ct);

            return visionResult.Success
                ? Result<string, string>.Success(visionResult.Description ?? "No description available")
                : Result<string, string>.Failure(visionResult.ErrorMessage ?? "Vision analysis failed for an unknown reason.");
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<string, string>.Failure($"Vision analysis failed: {ex.Message}");
        }
    }

    private record ScreenshotAnalysis(int Width, int Height, string Format, string Description);

    /// <summary>
    /// Detects UI elements in the current page using vision analysis.
    /// </summary>
    private async Task<string> DetectElementsAsync(CancellationToken ct)
    {
        // First capture a screenshot
        var screenshotResult = await _client.CallToolAsync("browser_take_screenshot", null, ct);
        if (screenshotResult.IsError)
        {
            return $"Failed to capture screenshot for element detection: {screenshotResult.Content}";
        }

        // Save the screenshot
        string? base64Data = ExtractBase64ImageData(screenshotResult.Content);
        if (string.IsNullOrEmpty(base64Data))
        {
            return "Could not extract image data for element detection";
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException)
        {
            return "Failed to decode screenshot for element detection";
        }

        string filename = $"detect_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_screenshotDirectory, filename);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);
        _lastScreenshotPath = filePath;

        // Use vision service to detect elements
        var detection = await _visionService.DetectScreenElementsAsync(filePath, ct);
        if (!detection.Success)
        {
            return $"Element detection failed: {detection.ErrorMessage}";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Detected UI Elements (using {_visionModel}):");
        sb.AppendLine($"Screenshot: {filePath}");
        sb.AppendLine();

        if (detection.Elements.Count > 0)
        {
            sb.AppendLine($"Found {detection.Elements.Count} interactive elements:");
            foreach (var elem in detection.Elements)
            {
                sb.AppendLine($"  [{elem.Type}] {elem.Label}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Raw Analysis:");
        sb.AppendLine(detection.RawDescription);

        return sb.ToString();
    }

    /// <summary>
    /// Extracts all visible text from the current page using OCR.
    /// </summary>
    private async Task<string> ExtractTextAsync(CancellationToken ct)
    {
        // Capture screenshot
        var screenshotResult = await _client.CallToolAsync("browser_take_screenshot", null, ct);
        if (screenshotResult.IsError)
        {
            return $"Failed to capture screenshot for text extraction: {screenshotResult.Content}";
        }

        string? base64Data = ExtractBase64ImageData(screenshotResult.Content);
        if (string.IsNullOrEmpty(base64Data))
        {
            return "Could not extract image data for text extraction";
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException)
        {
            return "Failed to decode screenshot for text extraction";
        }

        string filename = $"ocr_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_screenshotDirectory, filename);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);
        _lastScreenshotPath = filePath;

        // Use vision service for OCR
        var extraction = await _visionService.ExtractTextFromScreenshotAsync(filePath, ct);
        if (!extraction.Success)
        {
            return $"Text extraction failed: {extraction.ErrorMessage}";
        }

        return $"Extracted Text (using {_visionModel}):\n\n{extraction.ExtractedText}";
    }

    /// <summary>
    /// Suggests the next action based on the current page state and user's goal.
    /// </summary>
    private async Task<string> SuggestActionAsync(string goal, CancellationToken ct)
    {
        // Capture screenshot
        var screenshotResult = await _client.CallToolAsync("browser_take_screenshot", null, ct);
        if (screenshotResult.IsError)
        {
            return $"Failed to capture screenshot for action suggestion: {screenshotResult.Content}";
        }

        string? base64Data = ExtractBase64ImageData(screenshotResult.Content);
        if (string.IsNullOrEmpty(base64Data))
        {
            return "Could not extract image data for action suggestion";
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException)
        {
            return "Failed to decode screenshot for action suggestion";
        }

        string filename = $"suggest_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_screenshotDirectory, filename);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);
        _lastScreenshotPath = filePath;

        // Use vision service to suggest action
        var suggestion = await _visionService.SuggestNextActionAsync(filePath, goal, ct);
        if (!suggestion.Success)
        {
            return $"Action suggestion failed: {suggestion.ErrorMessage}";
        }

        return $"Action Suggestion (Goal: {goal})\nModel: {_visionModel}\n\n{suggestion.Suggestion}";
    }

    /// <summary>
    /// Validates that the current page matches expected state.
    /// </summary>
    private async Task<string> ValidatePageStateAsync(string[] expectations, CancellationToken ct)
    {
        if (expectations.Length == 0)
        {
            return "No expectations provided for validation. Use 'expectations' parameter with an array of strings describing what you expect to see.";
        }

        // Capture screenshot
        var screenshotResult = await _client.CallToolAsync("browser_take_screenshot", null, ct);
        if (screenshotResult.IsError)
        {
            return $"Failed to capture screenshot for validation: {screenshotResult.Content}";
        }

        string? base64Data = ExtractBase64ImageData(screenshotResult.Content);
        if (string.IsNullOrEmpty(base64Data))
        {
            return "Could not extract image data for validation";
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException)
        {
            return "Failed to decode screenshot for validation";
        }

        string filename = $"validate_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_screenshotDirectory, filename);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);
        _lastScreenshotPath = filePath;

        // Use vision service to validate
        var validation = await _visionService.ValidateScreenStateAsync(filePath, expectations, ct);
        if (!validation.Success)
        {
            return $"Validation failed: Details unavailable";
        }

        var status = validation.AllPassed ? "✅ ALL PASSED" : "❌ SOME FAILED";
        return $"Page Validation: {status}\nModel: {_visionModel}\n\nExpectations checked:\n{string.Join("\n", expectations.Select(e => $"  - {e}"))}\n\nResults:\n{validation.Details}";
    }
}
