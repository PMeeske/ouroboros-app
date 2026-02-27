// <copyright file="DynamicToolFactory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;

namespace Ouroboros.Application.Tools;

using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ouroboros.Providers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Ouroboros.Application.Mcp;
using Ouroboros.Application.Tools.CaptchaResolver;
using Ouroboros.Tools;

/// <summary>
/// Factory for dynamically generating, compiling, and registering tools at runtime.
/// Uses Roslyn for compilation and LLM for code generation.
/// </summary>
public partial class DynamicToolFactory
{
    private static readonly HttpClient _sharedHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        AutomaticDecompression = System.Net.DecompressionMethods.All
    }) { Timeout = TimeSpan.FromSeconds(30) };

    private readonly ToolAwareChatModel _llm;
    private readonly PlaywrightMcpTool? _playwrightMcpTool;
    private readonly CaptchaResolverChain _captchaResolver;
    private readonly AssemblyLoadContext _loadContext;
    private readonly List<MetadataReference> _references;
    private readonly string _storagePath;
    private int _toolCounter;

    /// <summary>
    /// Dangerous namespaces that must not appear in dynamically compiled tool code.
    /// These namespaces allow arbitrary file/process/network/emit access and are blocked
    /// to prevent sandbox escape in LLM-generated tools.
    /// </summary>
    private static readonly string[] BlockedNamespaces =
    [
        "System.IO",
        "System.Diagnostics",
        "System.Net",
        "System.Reflection.Emit",
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicToolFactory"/> class.
    /// </summary>
    /// <param name="llm">The LLM for generating tool code.</param>
    /// <param name="storagePath">Optional path to store generated tools.</param>
    /// <param name="playwrightMcpTool">Optional Playwright tool for browser automation.</param>
    public DynamicToolFactory(ToolAwareChatModel llm, string? storagePath = null, PlaywrightMcpTool? playwrightMcpTool = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _playwrightMcpTool = playwrightMcpTool;
        _loadContext = new AssemblyLoadContext("DynamicTools", isCollectible: true);
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ouroboros",
            "dynamic_tools");

        Directory.CreateDirectory(_storagePath);

        // Build reference assemblies for compilation
        _references = BuildReferences();

        // Initialize CAPTCHA resolver chain with available strategies
        // Use semantic decorator to enhance detection and provide intelligent guidance
        var visionResolver = new VisionCaptchaResolver(_playwrightMcpTool);
        var alternativeResolver = new AlternativeSearchResolver();

        _captchaResolver = new CaptchaResolverChain()
            .AddStrategy(new SemanticCaptchaResolverDecorator(visionResolver, _llm))
            .AddStrategy(new SemanticCaptchaResolverDecorator(alternativeResolver, _llm, useSemanticDetection: false))
            .AddStrategy(visionResolver)  // Fallback without semantic analysis
            .AddStrategy(alternativeResolver);
    }

    /// <summary>
    /// Gets the list of dynamically created tools.
    /// </summary>
    public List<(string Name, string Description, ITool Tool)> CreatedTools { get; } = new();

    /// <summary>
    /// Generates a new tool based on a natural language description.
    /// </summary>
    /// <param name="toolName">The desired name for the tool (e.g., "google_search").</param>
    /// <param name="description">Natural language description of what the tool should do.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the created tool or an error.</returns>
    public async Task<Result<ITool, string>> CreateToolAsync(
        string toolName,
        string description,
        CancellationToken ct = default)
    {
        try
        {
            // Sanitize tool name
            string safeName = SanitizeToolName(toolName);
            string className = $"{ToPascalCase(safeName)}Tool";

            // Generate tool code using LLM
            string codeResult = await GenerateToolCodeAsync(className, safeName, description, ct);
            if (string.IsNullOrWhiteSpace(codeResult))
            {
                return Result<ITool, string>.Failure("LLM failed to generate tool code");
            }

            // Extract code from markdown if present
            string code = ExtractCode(codeResult);

            // Ensure required using statements are present
            code = EnsureRequiredUsings(code);

            // SECURITY: Validate code does not use dangerous namespaces
            var securityViolation = ValidateCodeSecurity(code);
            if (securityViolation != null)
            {
                return Result<ITool, string>.Failure($"Security violation: {securityViolation}");
            }

            // Compile the tool
            var compileResult = CompileTool(code, className);
            if (!compileResult.IsSuccess)
            {
                // Try to fix compilation errors with LLM
                string fixPrompt = $@"The following C# code has compilation errors. Fix them and return ONLY the corrected code.
IMPORTANT: Make sure to include 'using Ouroboros.Core.Monads;' for the Result type.

ERRORS:
{compileResult.Error}

CODE:
```csharp
{code}
```";
                string fixedCode = await _llm.InnerModel.GenerateTextAsync(fixPrompt, ct);
                fixedCode = ExtractCode(fixedCode);
                fixedCode = EnsureRequiredUsings(fixedCode); // Ensure usings are present

                // SECURITY: Re-validate after LLM fix
                securityViolation = ValidateCodeSecurity(fixedCode);
                if (securityViolation != null)
                {
                    return Result<ITool, string>.Failure($"Security violation in fixed code: {securityViolation}");
                }

                compileResult = CompileTool(fixedCode, className);

                if (!compileResult.IsSuccess)
                {
                    return Result<ITool, string>.Failure($"Compilation failed: {compileResult.Error}");
                }

                code = fixedCode;
            }

            // Save the generated code for inspection
            string codePath = Path.Combine(_storagePath, $"{className}.cs");
            await File.WriteAllTextAsync(codePath, code, ct);

            // Create instance of the tool
            if (compileResult.Value is not ITool tool)
            {
                return Result<ITool, string>.Failure("Compiled type does not implement ITool");
            }

            CreatedTools.Add((safeName, description, tool));
            _toolCounter++;

            return Result<ITool, string>.Success(tool);
        }
        catch (Exception ex)
        {
            return Result<ITool, string>.Failure($"Tool creation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that generated code does not use dangerous namespaces.
    /// Returns null if safe, or a description of the violation.
    /// </summary>
    private static string? ValidateCodeSecurity(string code)
    {
        foreach (var ns in BlockedNamespaces)
        {
            // Check for using directives: "using System.IO;" or "using System.IO.Something;"
            if (Regex.IsMatch(code, $@"using\s+{Regex.Escape(ns)}(\s*;|\.\w)", RegexOptions.Multiline))
            {
                return $"Code uses blocked namespace '{ns}'. Dynamic tools may not use {ns} for security reasons.";
            }

            // Check for fully-qualified usage: "System.IO.File.ReadAllText"
            if (code.Contains($"{ns}."))
            {
                return $"Code references blocked namespace '{ns}'. Dynamic tools may not use {ns} for security reasons.";
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a simple delegate-based tool without full code generation.
    /// Faster but less flexible than full code generation.
    /// </summary>
    /// <param name="toolName">The name for the tool.</param>
    /// <param name="description">Description of what the tool does.</param>
    /// <param name="implementation">The implementation function.</param>
    /// <returns>The created tool.</returns>
    public ITool CreateSimpleTool(
        string toolName,
        string description,
        Func<string, Task<string>> implementation)
    {
        var tool = new DelegateTool(toolName, description, implementation);
        CreatedTools.Add((toolName, description, tool));
        return tool;
    }

    // Random instance for human-like timing simulation
    private static readonly Random _humanRng = new();

    /// <summary>
    /// Simulates human-like delay to avoid bot detection.
    /// </summary>
    private static async Task SimulateHumanDelayAsync(int minMs = 500, int maxMs = 2000)
    {
        int delay = _humanRng.Next(minMs, maxMs);
        await Task.Delay(delay);
    }

    /// <summary>
    /// Gets a randomized User-Agent string to simulate different browsers.
    /// </summary>
    private static string GetRandomUserAgent()
    {
        var userAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0"
        };
        return userAgents[_humanRng.Next(userAgents.Length)];
    }

    /// <summary>
    /// Configures an HttpRequestMessage with realistic browser-like headers.
    /// Thread-safe: sets headers per-request instead of on shared HttpClient.DefaultRequestHeaders.
    /// </summary>
    private static void ConfigureHumanLikeHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("User-Agent", GetRandomUserAgent());
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9,de;q=0.8");
        request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
        request.Headers.Add("DNT", "1");
        request.Headers.Add("Connection", "keep-alive");
        request.Headers.Add("Upgrade-Insecure-Requests", "1");
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        request.Headers.Add("Sec-Fetch-Site", "none");
        request.Headers.Add("Sec-Fetch-User", "?1");
        request.Headers.Add("Cache-Control", "max-age=0");
    }

    /// <summary>
    /// Creates a web search tool that searches the internet.
    /// </summary>
    /// <param name="searchProvider">The search provider (google, bing, duckduckgo).</param>
    /// <returns>A web search tool.</returns>
    public ITool CreateWebSearchTool(string searchProvider = "duckduckgo")
    {
        var tool = CreateSimpleTool(
            $"{searchProvider}_search",
            $"Search the web using {searchProvider} and return results",
            async (query) =>
            {
                var http = _sharedHttpClient;

                // Initial human-like delay before first request (simulates typing/thinking)
                await SimulateHumanDelayAsync(300, 800);

                // Try multiple search endpoints for DuckDuckGo
                var searchUrls = searchProvider.ToLowerInvariant() switch
                {
                    "google" => new[] { $"https://www.google.com/search?q={Uri.EscapeDataString(query)}" },
                    "bing" => new[] { $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}" },
                    _ => new[]
                    {
                        $"https://lite.duckduckgo.com/lite/?q={Uri.EscapeDataString(query)}",  // Lite version (less strict)
                        $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}", // HTML version
                        $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}&t=h_&ia=web" // Main site
                    }
                };

                foreach (var searchUrl in searchUrls)
                {
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                        ConfigureHumanLikeHeaders(request);

                        // Add referer for subsequent requests (simulates clicking through)
                        if (searchUrl != searchUrls[0])
                        {
                            request.Headers.Add("Referer", searchUrls[0]);

                            // Human-like delay between retry attempts
                            await SimulateHumanDelayAsync(1000, 3000);
                        }

                        var response = await http.SendAsync(request);

                        if (!response.IsSuccessStatusCode)
                        {
                            // Try next URL if this one fails
                            continue;
                        }

                        string html = await response.Content.ReadAsStringAsync();

                        // Check for CAPTCHA before extracting results
                        var captchaCheck = _captchaResolver.DetectCaptcha(html, searchUrl);
                        if (captchaCheck.IsCaptcha)
                        {
                            // CAPTCHA detected - try to resolve it
                            var resolution = await _captchaResolver.ResolveAsync(searchUrl, html);
                            if (resolution.Success && !string.IsNullOrWhiteSpace(resolution.ResolvedContent))
                            {
                                return resolution.ResolvedContent;
                            }

                            // Resolution failed - continue to next URL
                            continue;
                        }

                        // Extract text snippets from results
                        var results = ExtractSearchResults(html, searchProvider);
                        if (results.Count > 0)
                        {
                            return string.Join("\n\n", results.Take(5));
                        }
                    }
                    catch (HttpRequestException)
                    {
                        // Try next URL
                        continue;
                    }
                    catch (TaskCanceledException)
                    {
                        // Timeout, try next
                        continue;
                    }
                }

                // All primary URLs failed - use CAPTCHA resolver's alternative search strategy
                var fallbackUrl = $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}";
                var alternativeResult = await _captchaResolver.ResolveAsync(fallbackUrl, "Primary search failed");
                if (alternativeResult.Success && !string.IsNullOrWhiteSpace(alternativeResult.ResolvedContent))
                {
                    return alternativeResult.ResolvedContent;
                }

                // If CAPTCHA resolver also failed, try browser automation as last resort
                if (_playwrightMcpTool != null)
                {
                    try
                    {
                        // First, navigate to the page. This ensures the page is loaded before we screenshot.
                        var searchUrl = $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}&t=h_&ia=web";
                        var navArgs = new Dictionary<string, object>
                        {
                            { "action", "navigate" },
                            { "url", searchUrl }
                        };
                        var navJson = JsonSerializer.Serialize(navArgs);
                        await _playwrightMcpTool.InvokeAsync(navJson, CancellationToken.None); // We don't need the result, just the action

                        // Now, take a screenshot. We'll get the analysis in the next step.
                        var screenshotArgs = new Dictionary<string, object>
                        {
                            { "action", "screenshot" }
                        };
                        var screenshotJson = JsonSerializer.Serialize(screenshotArgs);
                        await _playwrightMcpTool.InvokeAsync(screenshotJson, CancellationToken.None);

                        // Get the clean vision analysis using the new internal method.
                        var visionResult = await _playwrightMcpTool.GetVisionAnalysisForLastScreenshotAsync(CancellationToken.None);

                        if (visionResult.IsSuccess)
                        {
                            var analysis = visionResult.Match(
                                success => success,
                                failure => string.Empty);

                            if (!string.IsNullOrWhiteSpace(analysis))
                            {
                                return $"Visually extracted results:\n{analysis}";
                            }
                        }
                        else
                        {
                            var error = visionResult.Match(
                                success => string.Empty,
                                failure => failure);
                            return $"Search failed. Vision analysis returned an error: {error}";
                        }
                    }
                    catch (Exception ex)
                    {
                        // Playwright tool also failed, fall through to the generic error.
                        return $"Search failed after multiple retries. Vision-based search failed with: {ex.Message}";
                    }
                }

                return "Search failed: All search providers returned errors or blocked the request. Try using the Playwright browser tool to search manually.";
            });

        return tool;
    }

    /// <summary>
    /// Creates a Google Search tool that uses SerpAPI (if key available) or DuckDuckGo fallback.
    /// </summary>
    /// <returns>A Google search tool.</returns>
    public ITool CreateGoogleSearchTool()
    {
        return CreateSimpleTool(
            "google_search",
            "Search the web using Google and return results with titles, URLs, and snippets",
            async (query) =>
            {
                // Simulate human typing/thinking delay
                await SimulateHumanDelayAsync(200, 600);

                var http = _sharedHttpClient;

                string? serpApiKey = Environment.GetEnvironmentVariable("SERPAPI_KEY");
                var results = new List<string>();

                try
                {
                    if (!string.IsNullOrEmpty(serpApiKey))
                    {
                        // Use SerpAPI for reliable Google results
                        string url = $"https://serpapi.com/search.json?q={Uri.EscapeDataString(query)}&api_key={serpApiKey}&num=10";
                        using var serpRequest = new HttpRequestMessage(HttpMethod.Get, url);
                        ConfigureHumanLikeHeaders(serpRequest);
                        var serpResponse = await http.SendAsync(serpRequest);
                        serpResponse.EnsureSuccessStatusCode();
                        string json = await serpResponse.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);

                        if (doc.RootElement.TryGetProperty("organic_results", out var organicEl))
                        {
                            foreach (var result in organicEl.EnumerateArray().Take(10))
                            {
                                string title = result.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                                string link = result.TryGetProperty("link", out var l) ? l.GetString() ?? "" : "";
                                string snippet = result.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";
                                results.Add($"🔍 {title}\n   URL: {link}\n   {snippet}");
                            }
                        }
                    }
                    else
                    {
                        // Fallback to DuckDuckGo HTML
                        string url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
                        using var ddgRequest = new HttpRequestMessage(HttpMethod.Get, url);
                        ConfigureHumanLikeHeaders(ddgRequest);
                        var ddgResponse = await http.SendAsync(ddgRequest);
                        ddgResponse.EnsureSuccessStatusCode();
                        string html = await ddgResponse.Content.ReadAsStringAsync();
                        results = ExtractSearchResults(html, "duckduckgo");
                    }

                    return results.Count > 0
                        ? string.Join("\n\n", results.Take(8))
                        : "No search results found.";
                }
                catch (Exception ex)
                {
                    return $"Google search failed: {ex.Message}";
                }
            });
    }

    /// <summary>
    /// Creates a URL fetcher tool.
    /// </summary>
    /// <returns>A URL fetcher tool.</returns>
    public ITool CreateUrlFetchTool()
    {
        return CreateSimpleTool(
            "fetch_url",
            "Fetch content from a URL and return the text",
            async (input) =>
            {
                string url = input?.Trim() ?? string.Empty;

                // Handle JSON input from orchestrator (e.g., {"url":"...","__sandboxed__":true})
                if (url.StartsWith("{") || url.StartsWith("'"))
                {
                    try
                    {
                        // Try to parse as JSON and extract 'url' field
                        string normalized = url.Replace("'", "\""); // Handle single quotes
                        using var doc = System.Text.Json.JsonDocument.Parse(normalized);
                        if (doc.RootElement.TryGetProperty("url", out var urlProp))
                        {
                            url = urlProp.GetString() ?? string.Empty;
                        }
                    }
                    catch
                    {
                        // Not valid JSON, continue with original input
                    }
                }

                // Validate URL is not empty
                if (string.IsNullOrWhiteSpace(url))
                {
                    return "Fetch failed: URL is required";
                }

                // Fix malformed URLs from LLM (e.g., "https: example.com path" -> "https://example.com/path")
                url = FixMalformedUrl(url);

                // Detect placeholder descriptions that LLMs sometimes generate instead of actual URLs
                string lower = url.ToLowerInvariant().Trim();
                if (lower.StartsWith("url of") ||
                    lower.StartsWith("the ") ||
                    lower.Contains(" of the ") ||
                    lower.Contains("from step") ||
                    lower.Contains("e.g.,") ||
                    lower.Contains("placeholder") ||
                    lower.Contains("result from"))
                {
                    return $"Fetch failed: The URL appears to be a placeholder description, not an actual URL. Got: '{url}'. Please provide a real URL like 'https://example.com'.";
                }

                // Validate URL is absolute
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsedUri))
                {
                    return $"Fetch failed: Invalid URL format. URL must be absolute (e.g., https://example.com). Got: {url}";
                }

                // Only allow http/https schemes
                if (parsedUri.Scheme != "http" && parsedUri.Scheme != "https")
                {
                    return $"Fetch failed: Only http and https URLs are supported. Got: {parsedUri.Scheme}";
                }

                // Simulate human-like delay before fetch
                await SimulateHumanDelayAsync(200, 500);

                var http = _sharedHttpClient;

                try
                {
                    using var fetchRequest = new HttpRequestMessage(HttpMethod.Get, parsedUri);
                    ConfigureHumanLikeHeaders(fetchRequest);
                    var response = await http.SendAsync(fetchRequest);
                    response.EnsureSuccessStatusCode();

                    // Read as bytes first, then decode as UTF-8 with fallback
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    string content;

                    try
                    {
                        content = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    catch
                    {
                        // Fallback to Latin-1 if UTF-8 fails
                        content = System.Text.Encoding.Latin1.GetString(bytes);
                    }

                    // Detect if content is still binary (not decompressed properly)
                    if (IsBinaryContent(content))
                    {
                        return "Fetch failed: Response appears to be binary or corrupted. The server may have returned compressed content that couldn't be decoded.";
                    }

                    // Basic HTML to text conversion
                    content = FetchScriptTagRegex().Replace(content, "");
                    content = FetchStyleTagRegex().Replace(content, "");
                    content = FetchHtmlTagRegex().Replace(content, " ");
                    content = FetchWhitespaceRegex().Replace(content, " ");
                    content = System.Net.WebUtility.HtmlDecode(content);

                    // Sanitize for embedding - remove non-printable characters
                    content = SanitizeForStorage(content);

                    // Truncate if too long
                    return content.Length > 5000 ? content[..5000] + "..." : content;
                }
                catch (Exception ex)
                {
                    return $"Fetch failed: {ex.Message}";
                }
            });
    }

    [GeneratedRegex(@"<script[^>]*>[\s\S]*?</script>", RegexOptions.IgnoreCase)]
    private static partial Regex FetchScriptTagRegex();

    [GeneratedRegex(@"<style[^>]*>[\s\S]*?</style>", RegexOptions.IgnoreCase)]
    private static partial Regex FetchStyleTagRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex FetchHtmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex FetchWhitespaceRegex();

    /// <summary>
    /// Creates a calculator tool.
    /// </summary>
    /// <returns>A calculator tool.</returns>
    public ITool CreateCalculatorTool()
    {
        return CreateSimpleTool(
            "calculator",
            "Evaluate mathematical expressions",
            (expression) =>
            {
                try
                {
                    // Simple expression evaluator using DataTable
                    var dt = new System.Data.DataTable();
                    var result = dt.Compute(expression, null);
                    return Task.FromResult(result?.ToString() ?? "undefined");
                }
                catch (Exception ex)
                {
                    return Task.FromResult($"Calculation error: {ex.Message}");
                }
            });
    }

    private async Task<string> GenerateToolCodeAsync(
        string className,
        string toolName,
        string description,
        CancellationToken ct)
    {
        string prompt = $@"Generate a C# class that implements the ITool interface for the following tool:

Tool Name: {toolName}
Description: {description}

Requirements:
1. Class name: {className}
2. Implement the ITool interface with these members:
   - string Name {{ get; }} => ""{toolName}""
   - string Description {{ get; }} => ""{description}""
   - string? JsonSchema {{ get; }} => null or a valid JSON schema for args
   - Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct)

3. Use HttpClient for any web requests
4. Handle errors gracefully and return Result<string, string>.Failure() on error
5. Return Result<string, string>.Success() with the result string on success

The Result type is: Result<TSuccess, TError> with static methods:
- Result<string, string>.Success(string value)
- Result<string, string>.Failure(string error)

IMPORTANT: You MUST include these exact using statements at the top:

```csharp
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Core.Monads;
using Ouroboros.Tools;

namespace Ouroboros.DynamicTools
{{
    public class {className} : ITool
    {{
        public string Name => ""{toolName}"";
        public string Description => ""{description}"";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {{
            try
            {{
                // Your implementation here
                return Result<string, string>.Success(""Result"");
            }}
            catch (Exception ex)
            {{
                return Result<string, string>.Failure(ex.Message);
            }}
        }}
    }}
}}
```

Generate ONLY the complete C# code, no explanations.";

        return await _llm.InnerModel.GenerateTextAsync(prompt, ct);
    }

    private Result<ITool?, string> CompileTool(string code, string className)
    {
        try
        {
            // Parse the code
            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            // Create compilation
            var assemblyName = $"DynamicTool_{_toolCounter}_{Guid.NewGuid():N}";
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release)
                    .WithPlatform(Platform.AnyCpu));

            // Emit to memory stream
            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())
                    .ToList();
                return Result<ITool?, string>.Failure(string.Join("\n", errors));
            }

            // Load assembly and create instance
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = _loadContext.LoadFromStream(ms);

            // Find the tool type
            var toolType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(ITool).IsAssignableFrom(t) && !t.IsAbstract);

            if (toolType == null)
            {
                return Result<ITool?, string>.Failure($"No ITool implementation found in compiled code");
            }

            var tool = (ITool?)Activator.CreateInstance(toolType);
            return tool != null
                ? Result<ITool?, string>.Success(tool)
                : Result<ITool?, string>.Failure("Failed to create tool instance");
        }
        catch (Exception ex)
        {
            return Result<ITool?, string>.Failure($"Compilation error: {ex.Message}");
        }
    }

    private List<MetadataReference> BuildReferences()
    {
        var refs = new List<MetadataReference>();

        // Get runtime assemblies
        var assemblies = new[]
        {
            typeof(object).Assembly,                           // System.Private.CoreLib
            typeof(Console).Assembly,                          // System.Console
            typeof(HttpClient).Assembly,                       // System.Net.Http
            typeof(Uri).Assembly,                              // System.Private.Uri
            typeof(Task).Assembly,                             // System.Threading.Tasks
            typeof(Enumerable).Assembly,                       // System.Linq
            typeof(JsonSerializer).Assembly,                   // System.Text.Json
            typeof(ITool).Assembly,                            // Ouroboros.Tools
            typeof(Result<,>).Assembly,                        // Ouroboros.Core (Result type)
            Assembly.Load("System.Runtime"),
            Assembly.Load("System.Collections"),
            Assembly.Load("netstandard"),
        };

        foreach (var asm in assemblies)
        {
            if (!string.IsNullOrEmpty(asm.Location))
            {
                refs.Add(MetadataReference.CreateFromFile(asm.Location));
            }
        }

        // Add additional runtime references
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var additionalRefs = new[]
        {
            "System.Threading.dll",
            "System.Threading.Tasks.dll",
            "System.Net.Primitives.dll",
            "System.Runtime.Extensions.dll",
            "System.ComponentModel.Primitives.dll",
        };

        foreach (var refName in additionalRefs)
        {
            var refPath = Path.Combine(runtimeDir, refName);
            if (File.Exists(refPath))
            {
                refs.Add(MetadataReference.CreateFromFile(refPath));
            }
        }

        return refs;
    }

    private static string SanitizeToolName(string name)
    {
        // Convert to snake_case and remove invalid chars
        var sb = new StringBuilder();
        foreach (char c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (c == ' ' || c == '-' || c == '_')
                sb.Append('_');
        }
        return sb.ToString().Trim('_');
    }

    private static string ToPascalCase(string snakeCase)
    {
        return string.Join("", snakeCase.Split('_')
            .Where(s => s.Length > 0)
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
    }
}
