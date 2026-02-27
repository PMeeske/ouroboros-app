namespace Ouroboros.Application.Mcp;

/// <summary>
/// Arguments for Playwright tool invocation.
/// </summary>
internal record PlaywrightArgs
{
    /// <summary>
    /// Gets or initializes the action to perform.
    /// </summary>
    public string Action { get; init; } = "";

    /// <summary>
    /// Gets or initializes the URL for navigation.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Gets or initializes the human-readable element description.
    /// </summary>
    public string? Element { get; init; }

    /// <summary>
    /// Gets or initializes the element reference from the snapshot (e.g., "e1", "e2").
    /// </summary>
    public string? Ref { get; init; }

    /// <summary>
    /// Gets or initializes the text to type.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets or initializes the JavaScript code to evaluate.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Gets or initializes whether to capture the full scrollable page for screenshots.
    /// </summary>
    public bool FullPage { get; init; }

    /// <summary>
    /// Gets or initializes the user's goal for action suggestion.
    /// </summary>
    public string? Goal { get; init; }

    /// <summary>
    /// Gets or initializes the expectations to validate against the page state.
    /// </summary>
    public string[]? Expectations { get; init; }

    /// <summary>
    /// Validates that a parameter value is not a placeholder description.
    /// LLMs sometimes output instructions like "URL of the result" instead of actual URLs.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The parameter name for error messages.</param>
    /// <returns>A validation result with error message if the value is a placeholder.</returns>
    public static (bool IsValid, string? Error) ValidateNotPlaceholder(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (true, null); // Empty is valid (will be caught by specific action validation)
        }

        // Common placeholder patterns that LLMs generate instead of actual values
        string lower = value.ToLowerInvariant().Trim();

        // Check for instruction-like text
        if (lower.StartsWith("url of") ||
            lower.StartsWith("ref of") ||
            lower.StartsWith("the ") ||
            lower.StartsWith("a ") ||
            lower.StartsWith("an ") ||
            lower.Contains(" of the ") ||
            lower.Contains("from step") ||
            lower.Contains("e.g.,") ||
            lower.Contains("for example") ||
            lower.Contains("placeholder") ||
            lower.Contains("insert ") ||
            lower.Contains("your ") ||
            lower.Contains("specify ") ||
            (lower.Contains("input") && lower.Contains("box")) ||
            (lower.Contains("search") && lower.Contains("button") && !lower.StartsWith("http")))
        {
            return (false, $"'{paramName}' appears to be a placeholder description, not an actual value. Got: '{value}'. Please provide the actual {paramName}.");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates all parameters in this args object.
    /// </summary>
    /// <returns>A validation result with error message if any parameter is invalid.</returns>
    public (bool IsValid, string? Error) Validate()
    {
        // Validate action is not empty
        if (string.IsNullOrWhiteSpace(Action))
        {
            return (false, "Action is required. Valid actions: navigate, snapshot, screenshot, click, type, hover, evaluate, detect_elements, extract_text, suggest_action, validate, list_tools");
        }

        (bool IsValid, string? Error) urlCheck = ValidateNotPlaceholder(Url, "url");
        if (!urlCheck.IsValid)
        {
            return urlCheck;
        }

        (bool IsValid, string? Error) refCheck = ValidateNotPlaceholder(Ref, "ref");
        if (!refCheck.IsValid)
        {
            return refCheck;
        }

        (bool IsValid, string? Error) elementCheck = ValidateNotPlaceholder(Element, "element");
        if (!elementCheck.IsValid)
        {
            return elementCheck;
        }

        // For 'ref', also validate it looks like an actual element reference (e.g., "e1", "e15")
        // Valid refs are typically "e" followed by digits, or similar short patterns
        if (!string.IsNullOrWhiteSpace(Ref) && (Ref.Length > 20 || Ref.Contains(' ')))
        {
            return (false, $"'ref' should be a short element reference like 'e1' or 'e15', not a description. Got: '{Ref}'");
        }

        return (true, null);
    }
}