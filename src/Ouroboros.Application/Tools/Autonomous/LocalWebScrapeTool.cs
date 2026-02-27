// <copyright file="LocalWebScrapeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Local web scraping tool that extracts clean content without external APIs.
/// Provides Firecrawl-like functionality using local HTML parsing.
/// </summary>
public class LocalWebScrapeTool : ITool
{
    /// <inheritdoc/>
    public string Name => "web_scrape";

    /// <inheritdoc/>
    public string Description => "Scrape a webpage locally and extract clean, readable content. No API key required. Input: URL to scrape.";

    /// <inheritdoc/>
    public string? JsonSchema => """{"type":"object","properties":{"url":{"type":"string","description":"URL to scrape"},"includeLinks":{"type":"boolean","description":"Include extracted links in output"},"maxLength":{"type":"integer","description":"Max content length (default 15000)"}},"required":["url"]}""";

    /// <inheritdoc/>
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        string url = input.Trim();
        bool includeLinks = false;
        int maxLength = 15000;

        // Try to parse JSON input
        try
        {
            using var doc = JsonDocument.Parse(input);
            if (doc.RootElement.TryGetProperty("url", out var urlEl))
                url = urlEl.GetString() ?? url;
            if (doc.RootElement.TryGetProperty("includeLinks", out var linksEl))
                includeLinks = linksEl.GetBoolean();
            if (doc.RootElement.TryGetProperty("maxLength", out var lengthEl))
                maxLength = lengthEl.GetInt32();
        }
        catch { /* Use raw input as URL */ }

        if (string.IsNullOrWhiteSpace(url))
            return Result<string, string>.Failure("No URL provided. Usage: web_scrape <url>");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            return Result<string, string>.Failure($"Invalid URL: {url}. Must be http or https.");

        try
        {
            return await ScrapeLocallyAsync(url, includeLinks, maxLength, ct);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Scrape failed: {ex.Message}");
        }
    }

    private static async Task<Result<string, string>> ScrapeLocallyAsync(
        string url, bool includeLinks, int maxLength, CancellationToken ct)
    {
        using var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };

        // Human-like headers
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        string html = await response.Content.ReadAsStringAsync(ct);
        var result = new StringBuilder();

        // Extract metadata
        string? title = ExtractMetaContent(html, @"<title[^>]*>([^<]+)</title>");
        string? description = ExtractMetaContent(html, @"<meta[^>]*name=[""']description[""'][^>]*content=[""']([^""']+)[""']")
                           ?? ExtractMetaContent(html, @"<meta[^>]*content=[""']([^""']+)[""'][^>]*name=[""']description[""']");
        string? ogTitle = ExtractMetaContent(html, @"<meta[^>]*property=[""']og:title[""'][^>]*content=[""']([^""']+)[""']");
        string? author = ExtractMetaContent(html, @"<meta[^>]*name=[""']author[""'][^>]*content=[""']([^""']+)[""']");

        // Build header
        result.AppendLine($"# {System.Net.WebUtility.HtmlDecode(ogTitle ?? title ?? "Untitled")}\n");
        result.AppendLine($"**Source:** {url}");
        if (!string.IsNullOrWhiteSpace(author))
            result.AppendLine($"**Author:** {author}");
        if (!string.IsNullOrWhiteSpace(description))
            result.AppendLine($"**Description:** {System.Net.WebUtility.HtmlDecode(description)}");
        result.AppendLine();

        // Extract main content
        string content = ExtractMainContent(html);

        // Clean up content
        content = CleanContent(content);

        // Truncate if needed
        if (content.Length > maxLength)
            content = content[..maxLength] + "\n\n...[content truncated]";

        result.AppendLine("---\n");
        result.AppendLine(content);

        // Extract links if requested
        if (includeLinks)
        {
            var links = ExtractLinks(html, url);
            if (links.Count > 0)
            {
                result.AppendLine("\n---\n## Extracted Links\n");
                foreach (var (linkText, linkUrl) in links.Take(20))
                {
                    result.AppendLine($"- [{linkText}]({linkUrl})");
                }
            }
        }

        return Result<string, string>.Success(result.ToString());
    }

    private static string? ExtractMetaContent(string html, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string ExtractMainContent(string html)
    {
        // Try to find main content areas
        string content = html;

        // Remove non-content elements first
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<script[^>]*>[\s\S]*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<style[^>]*>[\s\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<noscript[^>]*>[\s\S]*?</noscript>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<nav[^>]*>[\s\S]*?</nav>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<header[^>]*>[\s\S]*?</header>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<footer[^>]*>[\s\S]*?</footer>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<aside[^>]*>[\s\S]*?</aside>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<!--[\s\S]*?-->", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Try to extract article or main content
        var articleMatch = System.Text.RegularExpressions.Regex.Match(content,
            @"<article[^>]*>([\s\S]*?)</article>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (articleMatch.Success && articleMatch.Groups[1].Value.Length > 500)
            content = articleMatch.Groups[1].Value;
        else
        {
            var mainMatch = System.Text.RegularExpressions.Regex.Match(content,
                @"<main[^>]*>([\s\S]*?)</main>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mainMatch.Success && mainMatch.Groups[1].Value.Length > 500)
                content = mainMatch.Groups[1].Value;
        }

        // Convert common elements to markdown-style formatting
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<h1[^>]*>([^<]+)</h1>", "\n# $1\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<h2[^>]*>([^<]+)</h2>", "\n## $1\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<h3[^>]*>([^<]+)</h3>", "\n### $1\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<h[456][^>]*>([^<]+)</h[456]>", "\n**$1**\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<li[^>]*>", "\n\u2022 ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<br\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<p[^>]*>", "\n\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<strong[^>]*>([^<]+)</strong>", "**$1**", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<b[^>]*>([^<]+)</b>", "**$1**", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<em[^>]*>([^<]+)</em>", "*$1*", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<i[^>]*>([^<]+)</i>", "*$1*", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<code[^>]*>([^<]+)</code>", "`$1`", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<blockquote[^>]*>([^<]+)</blockquote>", "\n> $1\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Strip remaining tags
        content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", " ");

        return content;
    }

    private static string CleanContent(string content)
    {
        // Decode HTML entities
        content = System.Net.WebUtility.HtmlDecode(content);

        // Normalize whitespace
        content = System.Text.RegularExpressions.Regex.Replace(content, @"[ \t]+", " ");
        content = System.Text.RegularExpressions.Regex.Replace(content, @"\n{3,}", "\n\n");

        // Remove leading/trailing whitespace from lines
        var lines = content.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) || l == "");

        return string.Join("\n", lines).Trim();
    }

    private static List<(string Text, string Url)> ExtractLinks(string html, string baseUrl)
    {
        var links = new List<(string, string)>();
        var baseUri = new Uri(baseUrl);

        var linkRegex = new System.Text.RegularExpressions.Regex(
            @"<a[^>]*href=[""']([^""']+)[""'][^>]*>([^<]*)</a>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in linkRegex.Matches(html))
        {
            string href = match.Groups[1].Value;
            string text = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value).Trim();

            if (string.IsNullOrWhiteSpace(text) || text.Length > 100)
                continue;

            // Skip internal anchors, javascript, mailto
            if (href.StartsWith("#") || href.StartsWith("javascript:") || href.StartsWith("mailto:"))
                continue;

            // Resolve relative URLs
            try
            {
                var fullUrl = new Uri(baseUri, href).ToString();
                if (!links.Any(l => l.Item2 == fullUrl))
                    links.Add((text, fullUrl));
            }
            catch { /* Invalid URL */ }
        }

        return links;
    }
}
