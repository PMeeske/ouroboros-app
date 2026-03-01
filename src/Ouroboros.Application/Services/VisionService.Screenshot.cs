// <copyright file="VisionService.Screenshot.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

/// <summary>
/// Screenshot analysis, text extraction, action suggestion, comparison, and validation methods.
/// </summary>
public partial class VisionService
{
    /// <summary>
    /// Analyzes a screenshot to detect UI elements, buttons, links, and interactive components.
    /// </summary>
    /// <param name="imagePath">Path to the screenshot image.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Structured detection result with identified elements.</returns>
    public async Task<ScreenshotDetectionResult> DetectScreenElementsAsync(string imagePath, CancellationToken ct = default)
    {
        var prompt = @"Analyze this screenshot and identify all interactive UI elements. For each element, provide:
1. Type (button, link, input field, dropdown, checkbox, menu item, tab, icon)
2. Label or text content
3. Approximate position (top-left, top-center, top-right, middle-left, center, middle-right, bottom-left, bottom-center, bottom-right)
4. Visual state (enabled, disabled, focused, selected, hovered)

Format your response as a list with one element per line:
[TYPE] ""LABEL"" at POSITION - STATE

Also identify:
- The page title or header
- Any error messages or alerts
- Forms and their fields
- Navigation elements";

        var result = await AnalyzeImageAsync(imagePath, prompt, ct);
        if (!result.Success)
        {
            return new ScreenshotDetectionResult
            {
                Success = false,
                ErrorMessage = result.ErrorMessage,
            };
        }

        return ParseDetectionResult(result.Description ?? "");
    }

    /// <summary>
    /// Extracts all readable text from a screenshot using OCR.
    /// </summary>
    /// <param name="imagePath">Path to the screenshot image.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<TextExtractionResult> ExtractTextFromScreenshotAsync(string imagePath, CancellationToken ct = default)
    {
        var prompt = @"Extract ALL text visible in this screenshot. Include:
- Page titles and headers
- Button labels
- Menu items
- Form labels and input placeholders
- Error messages
- Status text
- Any other readable text

Organize the text by region:
HEADER:
NAVIGATION:
MAIN CONTENT:
SIDEBAR:
FOOTER:
DIALOGS/POPUPS:";

        var result = await AnalyzeImageAsync(imagePath, prompt, ct);

        return new TextExtractionResult
        {
            Success = result.Success,
            ExtractedText = result.Description ?? "",
            ErrorMessage = result.ErrorMessage,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Determines what action the user should take based on the current screen state.
    /// </summary>
    /// <param name="imagePath">Path to the screenshot.</param>
    /// <param name="goal">The user's goal or what they're trying to accomplish.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ActionSuggestionResult> SuggestNextActionAsync(string imagePath, string goal, CancellationToken ct = default)
    {
        var prompt = $@"The user's goal is: {goal}

Looking at this screenshot, suggest the next action to take. Provide:
1. RECOMMENDED ACTION: What to click, type, or do
2. TARGET ELEMENT: The specific button, link, or field to interact with
3. REASONING: Why this action helps achieve the goal
4. ALTERNATIVES: Other possible actions if the main one fails
5. WARNINGS: Any potential issues or confirmations to expect";

        var result = await AnalyzeImageAsync(imagePath, prompt, ct);

        return new ActionSuggestionResult
        {
            Success = result.Success,
            Suggestion = result.Description ?? "",
            ErrorMessage = result.ErrorMessage,
            Goal = goal,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Compares two screenshots to detect what changed.
    /// </summary>
    /// <param name="beforePath">Path to the before screenshot.</param>
    /// <param name="afterPath">Path to the after screenshot.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ScreenshotDiffResult> CompareScreenshotsAsync(string beforePath, string afterPath, CancellationToken ct = default)
    {
        // Analyze both screenshots
        var beforePrompt = "Describe the key elements, text, and state of this screenshot in detail.";
        var afterPrompt = "Describe the key elements, text, and state of this screenshot in detail.";

        var beforeResult = await AnalyzeImageAsync(beforePath, beforePrompt, ct);
        var afterResult = await AnalyzeImageAsync(afterPath, afterPrompt, ct);

        if (!beforeResult.Success || !afterResult.Success)
        {
            return new ScreenshotDiffResult
            {
                Success = false,
                ErrorMessage = "Failed to analyze one or both screenshots",
            };
        }

        // Compare the descriptions
        var comparePrompt = $@"Compare these two screenshot descriptions and identify all changes:

BEFORE:
{beforeResult.Description}

AFTER:
{afterResult.Description}

List the changes:
1. NEW ELEMENTS: What appeared
2. REMOVED ELEMENTS: What disappeared
3. CHANGED ELEMENTS: What was modified
4. TEXT CHANGES: Any text that changed
5. STATE CHANGES: Elements that changed state (enabled/disabled, selected/unselected, etc.)
6. OVERALL CHANGE: Brief summary of what happened";

        var diffResult = await AnalyzeTextAsync(comparePrompt, ct);

        return new ScreenshotDiffResult
        {
            Success = true,
            BeforeDescription = beforeResult.Description,
            AfterDescription = afterResult.Description,
            Changes = diffResult,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Validates that a page matches expected content or state.
    /// </summary>
    /// <param name="imagePath">Path to the screenshot.</param>
    /// <param name="expectations">List of expectations to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ValidationResult> ValidateScreenStateAsync(string imagePath, string[] expectations, CancellationToken ct = default)
    {
        var expectationList = string.Join("\n- ", expectations);
        var prompt = $@"Validate this screenshot against these expectations:
- {expectationList}

For each expectation, respond with:
[PASS] or [FAIL] - Explanation

Then provide an overall summary.";

        var result = await AnalyzeImageAsync(imagePath, prompt, ct);

        var passed = result.Description?.Contains("[PASS]", StringComparison.OrdinalIgnoreCase) == true;
        var failed = result.Description?.Contains("[FAIL]", StringComparison.OrdinalIgnoreCase) == true;

        return new ValidationResult
        {
            Success = result.Success,
            AllPassed = passed && !failed,
            Details = result.Description ?? "",
            Expectations = expectations,
            Timestamp = DateTime.UtcNow,
        };
    }

    private static ScreenshotDetectionResult ParseDetectionResult(string description)
    {
        var result = new ScreenshotDetectionResult
        {
            Success = true,
            RawDescription = description,
            Elements = new List<DetectedElement>(),
        };

        // Parse structured elements from description
        var lines = description.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[") && trimmed.Contains("]"))
            {
                var endBracket = trimmed.IndexOf(']');
                var elementType = trimmed[1..endBracket];

                // Extract label if quoted
                var labelStart = trimmed.IndexOf('"');
                var labelEnd = trimmed.LastIndexOf('"');
                var label = labelStart >= 0 && labelEnd > labelStart
                    ? trimmed[(labelStart + 1)..labelEnd]
                    : "";

                result.Elements.Add(new DetectedElement
                {
                    Type = elementType,
                    Label = label,
                    RawLine = trimmed,
                });
            }
        }

        return result;
    }
}
