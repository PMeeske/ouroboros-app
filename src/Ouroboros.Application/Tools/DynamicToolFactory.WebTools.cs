// <copyright file="DynamicToolFactory.WebTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;

namespace Ouroboros.Application.Tools;

using System.Text.Json;
using System.Text.RegularExpressions;
using Ouroboros.Tools;

/// <summary>
/// Web search and URL fetch tool creation for DynamicToolFactory.
/// </summary>
public partial class DynamicToolFactory
{
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
}
