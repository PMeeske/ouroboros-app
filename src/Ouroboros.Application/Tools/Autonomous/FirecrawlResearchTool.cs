// <copyright file="FirecrawlResearchTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Research tool that searches and scrapes web content using Firecrawl API.
/// </summary>
public class FirecrawlResearchTool : ITool
{
    /// <inheritdoc/>
    public string Name => "web_research";

    /// <inheritdoc/>
    public string Description => "Deep web Research using Firecrawl. PREFERRED for any web search or research task. Input: search query or URL to research.";

    /// <inheritdoc/>
    public string? JsonSchema => """{"type":"object","properties":{"query":{"type":"string","description":"Search query or URL to research"},"scrapeFirst":{"type":"boolean","description":"If true and input is a URL, scrape it directly"}},"required":["query"]}""";

    /// <inheritdoc/>
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        string query = input.Trim();

        // Try to parse JSON input
        try
        {
            using var doc = JsonDocument.Parse(input);
            if (doc.RootElement.TryGetProperty("query", out var queryEl))
                query = queryEl.GetString() ?? query;
        }
        catch { /* Use raw input as query */ }

        if (string.IsNullOrWhiteSpace(query))
            return Result<string, string>.Failure("No query provided");

        // Get API key from configuration or environment
        string? apiKey = ApiKeyProvider.GetApiKey("Firecrawl");

        // Check if input is a URL - if so, scrape it
        if (Uri.TryCreate(query, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            return await ScrapeUrlAsync(query, apiKey, ct);
        }

        // It's a search query - use Firecrawl Search API
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var result = await FirecrawlSearchAsync(query, apiKey, ct);
            if (result.IsSuccess)
                return result;

            // Firecrawl failed - fall back to free DuckDuckGo search
            return await DuckDuckGoFallbackSearchAsync(query, ct);
        }

        // No API key - use free DuckDuckGo search as fallback
        return await DuckDuckGoFallbackSearchAsync(query, ct);
    }

    private static async Task<Result<string, string>> DuckDuckGoFallbackSearchAsync(string query, CancellationToken ct)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            // Use DuckDuckGo HTML search (free, no API key needed)
            var encodedQuery = Uri.EscapeDataString(query);
            var response = await client.GetAsync($"https://html.duckduckgo.com/html/?q={encodedQuery}", ct);
            response.EnsureSuccessStatusCode();

            string html = await response.Content.ReadAsStringAsync(ct);

            // Parse results from HTML
            var results = new StringBuilder();

            // Extract result links and snippets using regex
            var linkRegex = new System.Text.RegularExpressions.Regex(
                @"<a[^>]*class=""result__a""[^>]*href=""([^""]+)""[^>]*>([^<]+)</a>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var snippetRegex = new System.Text.RegularExpressions.Regex(
                @"<a[^>]*class=""result__snippet""[^>]*>(.+?)</a>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            var linkMatches = linkRegex.Matches(html);
            var snippetMatches = snippetRegex.Matches(html);

            int count = 0;
            for (int i = 0; i < Math.Min(linkMatches.Count, 5); i++)
            {
                var linkMatch = linkMatches[i];
                string url = System.Net.WebUtility.HtmlDecode(linkMatch.Groups[1].Value);
                string title = System.Net.WebUtility.HtmlDecode(linkMatch.Groups[2].Value);

                // Skip DuckDuckGo internal links
                if (url.Contains("duckduckgo.com") || string.IsNullOrWhiteSpace(title))
                    continue;

                // Extract actual URL from DDG redirect
                if (url.StartsWith("//duckduckgo.com/l/?uddg="))
                {
                    var urlMatch = System.Text.RegularExpressions.Regex.Match(url, @"uddg=([^&]+)");
                    if (urlMatch.Success)
                        url = Uri.UnescapeDataString(urlMatch.Groups[1].Value);
                }

                count++;
                string snippet = "";
                if (i < snippetMatches.Count)
                {
                    snippet = System.Net.WebUtility.HtmlDecode(
                        System.Text.RegularExpressions.Regex.Replace(snippetMatches[i].Groups[1].Value, @"<[^>]+>", "")).Trim();
                    if (snippet.Length > 100) snippet = snippet[..100] + "...";
                }

                results.AppendLine($"{count}. {title}");
                if (!string.IsNullOrWhiteSpace(snippet))
                    results.AppendLine($"   {snippet}");
            }

            if (count == 0)
                return Result<string, string>.Success($"No results for: {query}");

            return Result<string, string>.Success($"Search results for '{query}':\n{results}".Trim());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"DuckDuckGo fallback search failed: {ex.Message}");
        }
    }

    private static async Task<Result<string, string>> FirecrawlSearchAsync(string query, string apiKey, CancellationToken ct)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            // Use Firecrawl's search endpoint
            var searchRequest = new
            {
                query = query,
                limit = 10,
                scrapeOptions = new
                {
                    formats = new[] { "markdown" },
                    onlyMainContent = true
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(searchRequest),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("https://api.firecrawl.dev/v1/search", jsonContent, ct);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(ct);
                return Result<string, string>.Failure($"Firecrawl search failed ({response.StatusCode}): {errorBody}");
            }

            string json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var results = new StringBuilder();
            results.AppendLine($"# Search Results: {query}\n");

            if (doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                int count = 0;
                foreach (var item in dataEl.EnumerateArray())
                {
                    count++;
                    string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    string url = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                    string description = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    string markdown = item.TryGetProperty("markdown", out var m) ? m.GetString() ?? "" : "";

                    results.AppendLine($"## {count}. {title}");
                    results.AppendLine($"**URL:** {url}");
                    if (!string.IsNullOrWhiteSpace(description))
                        results.AppendLine($"**Description:** {description}");

                    // Include snippet of markdown content if available
                    if (!string.IsNullOrWhiteSpace(markdown))
                    {
                        string snippet = markdown.Length > 500 ? markdown[..500] + "..." : markdown;
                        results.AppendLine($"\n{snippet}");
                    }

                    results.AppendLine();
                }

                if (count == 0)
                    return Result<string, string>.Success($"No search results found for: {query}");
            }

            return Result<string, string>.Success(results.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Firecrawl search failed: {ex.Message}");
        }
    }

    private static async Task<Result<string, string>> ScrapeUrlAsync(string url, string? apiKey, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Use Firecrawl scrape API
            var scrapeTool = new FirecrawlScrapeTool();
            return await scrapeTool.InvokeAsync(url, ct);
        }

        // Fallback to basic fetch with decompression
        try
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync(ct);

            // Basic HTML stripping
            content = System.Text.RegularExpressions.Regex.Replace(content, @"<script[^>]*>[\s\S]*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content, @"<style[^>]*>[\s\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", " ");
            content = System.Net.WebUtility.HtmlDecode(content);
            content = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ").Trim();

            return Result<string, string>.Success(content.Length > 20000 ? content[..20000] + "...[truncated]" : content);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to fetch URL: {ex.Message}");
        }
    }
}
