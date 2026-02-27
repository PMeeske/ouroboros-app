// <copyright file="PlaywrightMcpTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Services;

namespace Ouroboros.Application.Mcp;

/// <summary>
/// Playwright browser automation tool that connects to the Playwright MCP server.
/// Provides web scraping, browser automation, and testing capabilities to Ouroboros.
/// Uses accessibility snapshots with element references (e.g., ref=e1, ref=e2) for reliable interaction.
///
/// Vision analysis is in PlaywrightMcpTool.Vision.cs.
/// Browser interaction (click, type, hover, evaluate) is in PlaywrightMcpTool.Interaction.cs.
/// </summary>
public partial class PlaywrightMcpTool : ITool, IAsyncDisposable
{
    private readonly McpClient _client;
    private readonly string _screenshotDirectory;
    private readonly VisionService _visionService;
    private readonly string _visionModel;
    private bool _initialized;
    private List<McpToolInfo>? _availableTools;
    private string? _lastScreenshotPath;

    /// <summary>
    /// Gets the path to the last captured screenshot, or null if no screenshots taken.
    /// </summary>
    public string? LastScreenshotPath => _lastScreenshotPath;

    /// <summary>
    /// Gets the directory where screenshots are saved.
    /// </summary>
    public string ScreenshotDirectory => _screenshotDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaywrightMcpTool"/> class.
    /// </summary>
    /// <param name="headless">If true, browser runs in headless mode (invisible). Default is false (visible).</param>
    /// <param name="browser">Browser to use: chrome, firefox, webkit, msedge. Default is chrome.</param>
    /// <param name="screenshotDirectory">Directory to save screenshots. Default is temp folder.</param>
    /// <param name="visionModel">Vision model to use for screenshot analysis. Default is ministral.</param>
    /// <param name="ollamaEndpoint">Ollama endpoint for vision model. Default is localhost:11434.</param>
    public PlaywrightMcpTool(
        bool headless = false,
        string browser = "edge",
        string? screenshotDirectory = null,
        string visionModel = "minicpm-v",
        string ollamaEndpoint = Configuration.DefaultEndpoints.Ollama)
    {
        // Use -y to auto-confirm package installation and @latest to get current version
        var args = new List<string> { "-y", "@playwright/mcp@latest", "--browser", browser };
        if (headless)
        {
            args.Add("--headless");
        }

        _client = new McpClient("npx", args.ToArray());

        // Setup screenshot directory
        _screenshotDirectory = screenshotDirectory ?? Path.Combine(Path.GetTempPath(), "ouroboros_screenshots");
        Directory.CreateDirectory(_screenshotDirectory);

        // Setup vision service with Ministral model
        _visionModel = visionModel;
        _visionService = new VisionService(new VisionConfig
        {
            Backend = VisionBackend.Ollama,
            OllamaEndpoint = ollamaEndpoint,
            OllamaVisionModel = visionModel,
        });
    }

    /// <inheritdoc/>
    public string Name => "playwright";

    /// <inheritdoc/>
    public string Description =>
        "Browser automation using Playwright with AI vision. WORKFLOW: 1) Use 'navigate' to go to a URL, " +
        "2) Use 'snapshot' to get the accessibility tree with element refs like [ref=e1], " +
        "3) Use 'click' or 'type' with the element description and ref. " +
        "VISION ACTIONS: 'screenshot' captures and analyzes page, 'detect_elements' finds UI components, " +
        "'extract_text' performs OCR, 'suggest_action' recommends next step, 'validate' checks page state. " +
        "Available actions: navigate, snapshot, screenshot, detect_elements, extract_text, suggest_action, validate, click, type, hover, evaluate, list_tools.";

    /// <inheritdoc/>
    public string? JsonSchema => """
        {
            "type": "object",
            "properties": {
                "action": {
                    "type": "string",
                    "description": "The action to perform",
                    "enum": ["navigate", "snapshot", "screenshot", "detect_elements", "extract_text", "suggest_action", "validate", "click", "type", "hover", "evaluate", "list_tools"]
                },
                "url": {
                    "type": "string",
                    "description": "URL to navigate to (for navigate action)"
                },
                "element": {
                    "type": "string",
                    "description": "Human-readable element description, e.g., 'Search button' or 'Email input'"
                },
                "ref": {
                    "type": "string",
                    "description": "Element reference from snapshot, e.g., 'e1', 'e2', 'e15'. Get this from the snapshot output."
                },
                "text": {
                    "type": "string",
                    "description": "Text to type (for type action)"
                },
                "code": {
                    "type": "string",
                    "description": "JavaScript code to evaluate (for evaluate action)"
                },
                "fullPage": {
                    "type": "boolean",
                    "description": "Capture full scrollable page for screenshots (default: false)"
                },
                "goal": {
                    "type": "string",
                    "description": "User's goal for suggest_action - what are they trying to accomplish?"
                },
                "expectations": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "List of expectations to validate against the current page state"
                }
            },
            "required": ["action"]
        }
        """;

    /// <summary>
    /// Initializes the Playwright MCP connection.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized && _client.IsConnected) return;

        await _client.ConnectAsync(ct);
        _availableTools = await _client.ListToolsAsync(ct);

        // Log what tools we got (useful for debugging)
        if (_availableTools.Count == 0)
        {
            Console.WriteLine("  ⚠ Playwright MCP: No tools returned from server");
        }

        _initialized = true;
    }

    /// <summary>
    /// Gets the available Playwright tools.
    /// </summary>
    public IReadOnlyList<McpToolInfo> AvailableTools => _availableTools ?? [];

    /// <inheritdoc/>
    public async Task<Result<string, string>> InvokeAsync(string args, CancellationToken ct = default)
    {
        try
        {
            // Ensure connection is healthy before each call
            if (!_initialized || !_client.IsConnected)
            {
                _initialized = false;
                await InitializeAsync(ct);
            }

            // Handle various input formats
            PlaywrightArgs? parsed = null;
            string trimmedArgs = args?.Trim() ?? "";

            // Try JSON parsing first
            if (trimmedArgs.StartsWith("{"))
            {
                try
                {
                    parsed = JsonSerializer.Deserialize<PlaywrightArgs>(trimmedArgs);
                }
                catch (JsonException)
                {
                    // Fall through to other parsing methods
                }
            }

            // If not JSON or JSON parsing failed, try to interpret the input
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Action))
            {
                // Check if it's a URL - auto-navigate
                if (Uri.TryCreate(trimmedArgs, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    parsed = new PlaywrightArgs { Action = "navigate", Url = trimmedArgs };
                }
                // Check for simple action keywords
                else if (trimmedArgs.Equals("snapshot", StringComparison.OrdinalIgnoreCase) ||
                         trimmedArgs.Equals("screenshot", StringComparison.OrdinalIgnoreCase) ||
                         trimmedArgs.Equals("list_tools", StringComparison.OrdinalIgnoreCase))
                {
                    parsed = new PlaywrightArgs { Action = trimmedArgs.ToLowerInvariant() };
                }
                else if (string.IsNullOrWhiteSpace(trimmedArgs))
                {
                    return Result<string, string>.Failure(
                        "No input provided. Usage examples:\n" +
                        "  {\"action\": \"navigate\", \"url\": \"https://example.com\"}\n" +
                        "  {\"action\": \"snapshot\"}\n" +
                        "  {\"action\": \"click\", \"ref\": \"e1\"}\n" +
                        "  Or just pass a URL to auto-navigate.");
                }
                else
                {
                    return Result<string, string>.Failure(
                        $"Could not parse input: '{trimmedArgs}'\n" +
                        "Expected JSON with 'action' field. Valid actions: navigate, snapshot, screenshot, click, type, hover, evaluate, detect_elements, extract_text, suggest_action, validate, list_tools");
                }
            }

            // Validate that parameters are actual values, not placeholder descriptions
            (bool isValid, string? validationError) = parsed.Validate();
            if (!isValid)
            {
                return Result<string, string>.Failure(validationError!);
            }

            // Map high-level actions to Playwright MCP tool calls
            string result = parsed.Action.ToLowerInvariant() switch
            {
                "list_tools" => await ListAvailableToolsAsync(),
                "navigate" => await NavigateAsync(parsed.Url ?? "", ct),
                "snapshot" => await GetSnapshotAsync(ct),
                "screenshot" => await TakeScreenshotAsync(parsed.FullPage, ct),
                "detect_elements" => await DetectElementsAsync(ct),
                "extract_text" => await ExtractTextAsync(ct),
                "suggest_action" => await SuggestActionAsync(parsed.Goal ?? "complete the current task", ct),
                "validate" => await ValidatePageStateAsync(parsed.Expectations ?? Array.Empty<string>(), ct),
                "click" => await ClickAsync(parsed.Element ?? "", parsed.Ref ?? "", ct),
                "type" => await TypeAsync(parsed.Element ?? "", parsed.Ref ?? "", parsed.Text ?? "", ct),
                "hover" => await HoverAsync(parsed.Element ?? "", parsed.Ref ?? "", ct),
                "evaluate" => await EvaluateAsync(parsed.Code ?? "", ct),
                _ => await CallRawToolAsync(parsed.Action, parsed, ct),
            };

            // Sanitize output - remove binary/corrupted data that could confuse the LLM
            result = SanitizeOutput(result);

            return Result<string, string>.Success(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Not connected") || ex.Message.Contains("No response"))
        {
            // Server crashed or disconnected - try to reconnect
            _initialized = false;
            return Result<string, string>.Failure($"Playwright MCP server disconnected. Please retry the action. ({ex.Message})");
        }
        catch (Exception ex)
        {
            string connectionStatus = _client.IsConnected ? "connected" : "disconnected";
            return Result<string, string>.Failure($"Playwright error ({connectionStatus}): {ex.Message}");
        }
    }

    private Task<string> ListAvailableToolsAsync()
    {
        if (_availableTools == null || _availableTools.Count == 0)
        {
            return Task.FromResult("No tools available. Playwright MCP server may not be running.");
        }

        var toolList = string.Join("\n", _availableTools.Select(t => $"- {t.Name}: {t.Description}"));
        return Task.FromResult($"Available Playwright tools:\n{toolList}");
    }

    private async Task<string> NavigateAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(url))
        {
            return "Error: URL is required for navigate action";
        }

        // Verify the tool exists
        if (_availableTools?.Any(t => t.Name == "browser_navigate") != true)
        {
            var available = _availableTools != null
                ? string.Join(", ", _availableTools.Select(t => t.Name))
                : "none";
            return $"Error: browser_navigate tool not available. Available tools: {available}";
        }

        // Human-like delay before navigation (simulates typing URL or clicking link)
        await Task.Delay(Random.Shared.Next(400, 900), ct);

        var result = await _client.CallToolAsync(
            "browser_navigate",
            new Dictionary<string, object?> { ["url"] = url },
            ct);

        // Wait for page to settle (human would wait to see page load)
        if (!result.IsError)
        {
            await Task.Delay(Random.Shared.Next(800, 1500), ct);
        }

        return result.IsError ? $"Navigation failed: {result.Content}" : $"Navigated to {url}\n\n{result.Content}";
    }

    private async Task<string> GetSnapshotAsync(CancellationToken ct)
    {
        // Small delay to simulate looking at the page
        await Task.Delay(Random.Shared.Next(200, 400), ct);

        var result = await _client.CallToolAsync("browser_snapshot", null, ct);
        return result.IsError ? $"Snapshot failed: {result.Content}" : result.Content;
    }

    private async Task<string> TakeScreenshotAsync(bool fullPage, CancellationToken ct)
    {
        var args = fullPage ? new Dictionary<string, object?> { ["fullPage"] = true } : null;
        var result = await _client.CallToolAsync("browser_take_screenshot", args, ct);
        if (result.IsError)
        {
            return $"Screenshot failed: {result.Content}";
        }

        // Process and understand the screenshot
        return await ProcessScreenshotAsync(result.Content, ct);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        _visionService.Dispose();
        await Task.CompletedTask;
    }
}
