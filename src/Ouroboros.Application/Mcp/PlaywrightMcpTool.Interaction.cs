// <copyright file="PlaywrightMcpTool.Interaction.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Mcp;

/// <summary>
/// Browser interaction methods: click, type, hover, evaluate, raw tool calls, and output sanitization.
/// </summary>
public partial class PlaywrightMcpTool
{
    private async Task<string> ClickAsync(string element, string @ref, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(@ref))
        {
            return "Error: 'ref' is required for click action. Use 'snapshot' first to get element refs like 'e1', 'e2'.";
        }

        // Human-like delay before clicking (simulates mouse movement)
        await Task.Delay(Random.Shared.Next(200, 600), ct);

        var result = await _client.CallToolAsync(
            "browser_click",
            new Dictionary<string, object?>
            {
                ["element"] = string.IsNullOrEmpty(element) ? "element" : element,
                ["ref"] = @ref,
            },
            ct);

        return result.IsError ? $"Click failed: {result.Content}" : $"Clicked {element} [{@ref}]\n\n{result.Content}";
    }

    private async Task<string> TypeAsync(string element, string @ref, string text, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(@ref))
        {
            return "Error: 'ref' is required for type action. Use 'snapshot' first to get element refs like 'e1', 'e2'.";
        }

        if (string.IsNullOrEmpty(text))
        {
            return "Error: 'text' is required for type action.";
        }

        // Human-like delay before typing (simulates focusing on input)
        await Task.Delay(Random.Shared.Next(300, 700), ct);

        var result = await _client.CallToolAsync(
            "browser_type",
            new Dictionary<string, object?>
            {
                ["element"] = string.IsNullOrEmpty(element) ? "input" : element,
                ["ref"] = @ref,
                ["text"] = text,
                // Ask Playwright MCP to type slowly like a human
                ["slowly"] = true,
            },
            ct);

        return result.IsError ? $"Type failed: {result.Content}" : $"Typed into {element} [{@ref}]\n\n{result.Content}";
    }

    private async Task<string> HoverAsync(string element, string @ref, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(@ref))
        {
            return "Error: 'ref' is required for hover action. Use 'snapshot' first to get element refs.";
        }

        // Human-like delay for mouse movement to element
        await Task.Delay(Random.Shared.Next(150, 400), ct);

        var result = await _client.CallToolAsync(
            "browser_hover",
            new Dictionary<string, object?>
            {
                ["element"] = string.IsNullOrEmpty(element) ? "element" : element,
                ["ref"] = @ref,
            },
            ct);

        return result.IsError ? $"Hover failed: {result.Content}" : $"Hovered over {element} [{@ref}]\n\n{result.Content}";
    }

    private async Task<string> EvaluateAsync(string code, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(code))
        {
            return "Error: 'code' is required for evaluate action";
        }

        var result = await _client.CallToolAsync(
            "browser_evaluate",
            new Dictionary<string, object?> { ["function"] = code },
            ct);

        return result.IsError ? $"Evaluate failed: {result.Content}" : result.Content;
    }

    private async Task<string> CallRawToolAsync(string toolName, PlaywrightArgs args, CancellationToken ct)
    {
        // Validate tool name before calling MCP server
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return "Error: Tool name cannot be empty. Valid actions: navigate, snapshot, screenshot, click, type, hover, evaluate, detect_elements, extract_text, suggest_action, validate, list_tools";
        }

        // Check if the tool exists on the MCP server
        if (_availableTools != null && !_availableTools.Any(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase)))
        {
            var availableNames = _availableTools.Select(t => t.Name).ToList();
            var suggestion = availableNames.FirstOrDefault(n => n.Contains(toolName, StringComparison.OrdinalIgnoreCase));
            var suggestionMsg = suggestion != null ? $" Did you mean '{suggestion}'?" : "";
            return $"Error: Unknown action '{toolName}'.{suggestionMsg} Valid Playwright actions: navigate, snapshot, screenshot, click, type, hover, evaluate. Raw MCP tools: {string.Join(", ", availableNames)}";
        }

        // Build arguments dictionary from the PlaywrightArgs
        var arguments = new Dictionary<string, object?>();

        if (!string.IsNullOrEmpty(args.Url)) arguments["url"] = args.Url;
        if (!string.IsNullOrEmpty(args.Element)) arguments["element"] = args.Element;
        if (!string.IsNullOrEmpty(args.Ref)) arguments["ref"] = args.Ref;
        if (!string.IsNullOrEmpty(args.Text)) arguments["text"] = args.Text;
        if (!string.IsNullOrEmpty(args.Code)) arguments["function"] = args.Code;

        var result = await _client.CallToolAsync(toolName, arguments, ct);
        return result.IsError ? $"Tool call failed: {result.Content}" : SanitizeOutput(result.Content);
    }

    /// <summary>
    /// Sanitizes output to remove binary data, excessive whitespace, or corrupted content.
    /// </summary>
    private static string SanitizeOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return output;
        }

        // Check for binary/corrupted data (high ratio of non-printable characters)
        int nonPrintableCount = output.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && !char.IsPunctuation(c) && c != '\n' && c != '\r' && c != '\t');
        if (nonPrintableCount > output.Length * 0.3 && output.Length > 100)
        {
            // More than 30% non-printable characters - likely binary or corrupted
            return $"[Binary/corrupted data detected - {output.Length} bytes. Content may be an image or encoded data.]";
        }

        // Limit output length to prevent overwhelming the LLM context
        const int maxLength = 50000;
        if (output.Length > maxLength)
        {
            return output[..maxLength] + $"\n\n[Output truncated - showing first {maxLength} of {output.Length} characters]";
        }

        // Clean up excessive whitespace
        output = ExcessiveHorizontalWhitespaceRegex().Replace(output, "    ");
        output = ExcessiveNewlinesRegex().Replace(output, "\n\n\n");

        return output;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"[ \t]{20,}")]
    private static partial System.Text.RegularExpressions.Regex ExcessiveHorizontalWhitespaceRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"(\r?\n){5,}")]
    private static partial System.Text.RegularExpressions.Regex ExcessiveNewlinesRegex();
}
