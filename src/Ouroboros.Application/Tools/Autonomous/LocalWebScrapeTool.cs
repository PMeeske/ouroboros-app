// <copyright file="LocalWebScrapeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Local web scraping tool that extracts clean content without external APIs.
/// Provides Firecrawl-like functionality using local HTML parsing.
/// </summary>
public partial class LocalWebScrapeTool : ITool
{
    private static readonly HttpClient _sharedHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5
    }) { Timeout = TimeSpan.FromSeconds(45) };

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
        catch (System.Text.Json.JsonException) { /* Use raw input as URL */ }

        if (string.IsNullOrWhiteSpace(url))
            return Result<string, string>.Failure("No URL provided. Usage: web_scrape <url>");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            return Result<string, string>.Failure($"Invalid URL: {url}. Must be http or https.");

        // SSRF protection: block private/internal IPs
        if (!await UrlValidator.IsUrlSafeAsync(url))
            return Result<string, string>.Failure($"Blocked: URL '{url}' resolves to a private or internal IP address.");

        try
        {
            return await ScrapeLocallyAsync(url, includeLinks, maxLength, ct);
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Scrape failed: {ex.Message}");
        }
    }

    private static async Task<Result<string, string>> ScrapeLocallyAsync(
        string url, bool includeLinks, int maxLength, CancellationToken ct)
    {
        var client = _sharedHttpClient;

        // Use HttpRequestMessage for thread-safe per-request headers
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        string html = await response.Content.ReadAsStringAsync(ct);
        var result = new StringBuilder();

        // Extract metadata
        string? title = ExtractMetaContent(html, TitleRegex());
        string? description = ExtractMetaContent(html, MetaDescriptionRegex())
                           ?? ExtractMetaContent(html, MetaDescriptionAltRegex());
        string? ogTitle = ExtractMetaContent(html, OgTitleRegex());
        string? author = ExtractMetaContent(html, AuthorRegex());

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

    private static string? ExtractMetaContent(string html, Regex pattern)
    {
        var match = pattern.Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string ExtractMainContent(string html)
    {
        // Try to find main content areas
        string content = html;

        // Remove non-content elements first
        content = ScriptTagRegex().Replace(content, "");
        content = StyleTagRegex().Replace(content, "");
        content = NoscriptTagRegex().Replace(content, "");
        content = NavTagRegex().Replace(content, "");
        content = HeaderTagRegex().Replace(content, "");
        content = FooterTagRegex().Replace(content, "");
        content = AsideTagRegex().Replace(content, "");
        content = HtmlCommentRegex().Replace(content, "");

        // Try to extract article or main content
        var articleMatch = ArticleTagRegex().Match(content);
        if (articleMatch.Success && articleMatch.Groups[1].Value.Length > 500)
            content = articleMatch.Groups[1].Value;
        else
        {
            var mainMatch = MainTagRegex().Match(content);
            if (mainMatch.Success && mainMatch.Groups[1].Value.Length > 500)
                content = mainMatch.Groups[1].Value;
        }

        // Convert common elements to markdown-style formatting
        content = H1TagRegex().Replace(content, "\n# $1\n");
        content = H2TagRegex().Replace(content, "\n## $1\n");
        content = H3TagRegex().Replace(content, "\n### $1\n");
        content = H456TagRegex().Replace(content, "\n**$1**\n");
        content = LiTagRegex().Replace(content, "\n\u2022 ");
        content = BrTagRegex().Replace(content, "\n");
        content = PTagRegex().Replace(content, "\n\n");
        content = StrongTagRegex().Replace(content, "**$1**");
        content = BoldTagRegex().Replace(content, "**$1**");
        content = EmTagRegex().Replace(content, "*$1*");
        content = ItalicTagRegex().Replace(content, "*$1*");
        content = CodeTagRegex().Replace(content, "`$1`");
        content = BlockquoteTagRegex().Replace(content, "\n> $1\n");

        // Strip remaining tags
        content = AnyHtmlTagRegex().Replace(content, " ");

        return content;
    }

    private static string CleanContent(string content)
    {
        // Decode HTML entities
        content = System.Net.WebUtility.HtmlDecode(content);

        // Normalize whitespace
        content = HorizontalWhitespaceRegex().Replace(content, " ");
        content = ExcessiveNewlinesRegex().Replace(content, "\n\n");

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

        foreach (Match match in AnchorTagRegex().Matches(html))
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
            catch (UriFormatException) { /* Invalid URL */ }
        }

        return links;
    }

    // === Generated Regex Patterns ===

    // Metadata extraction
    [GeneratedRegex(@"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<meta[^>]*name=[""']description[""'][^>]*content=[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex MetaDescriptionRegex();

    [GeneratedRegex(@"<meta[^>]*content=[""']([^""']+)[""'][^>]*name=[""']description[""']", RegexOptions.IgnoreCase)]
    private static partial Regex MetaDescriptionAltRegex();

    [GeneratedRegex(@"<meta[^>]*property=[""']og:title[""'][^>]*content=[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex OgTitleRegex();

    [GeneratedRegex(@"<meta[^>]*name=[""']author[""'][^>]*content=[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorRegex();

    // Non-content element removal
    [GeneratedRegex(@"<script[^>]*>[\s\S]*?</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTagRegex();

    [GeneratedRegex(@"<style[^>]*>[\s\S]*?</style>", RegexOptions.IgnoreCase)]
    private static partial Regex StyleTagRegex();

    [GeneratedRegex(@"<noscript[^>]*>[\s\S]*?</noscript>", RegexOptions.IgnoreCase)]
    private static partial Regex NoscriptTagRegex();

    [GeneratedRegex(@"<nav[^>]*>[\s\S]*?</nav>", RegexOptions.IgnoreCase)]
    private static partial Regex NavTagRegex();

    [GeneratedRegex(@"<header[^>]*>[\s\S]*?</header>", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderTagRegex();

    [GeneratedRegex(@"<footer[^>]*>[\s\S]*?</footer>", RegexOptions.IgnoreCase)]
    private static partial Regex FooterTagRegex();

    [GeneratedRegex(@"<aside[^>]*>[\s\S]*?</aside>", RegexOptions.IgnoreCase)]
    private static partial Regex AsideTagRegex();

    [GeneratedRegex(@"<!--[\s\S]*?-->", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlCommentRegex();

    // Content area extraction
    [GeneratedRegex(@"<article[^>]*>([\s\S]*?)</article>", RegexOptions.IgnoreCase)]
    private static partial Regex ArticleTagRegex();

    [GeneratedRegex(@"<main[^>]*>([\s\S]*?)</main>", RegexOptions.IgnoreCase)]
    private static partial Regex MainTagRegex();

    // HTML to markdown conversion
    [GeneratedRegex(@"<h1[^>]*>([^<]+)</h1>", RegexOptions.IgnoreCase)]
    private static partial Regex H1TagRegex();

    [GeneratedRegex(@"<h2[^>]*>([^<]+)</h2>", RegexOptions.IgnoreCase)]
    private static partial Regex H2TagRegex();

    [GeneratedRegex(@"<h3[^>]*>([^<]+)</h3>", RegexOptions.IgnoreCase)]
    private static partial Regex H3TagRegex();

    [GeneratedRegex(@"<h[456][^>]*>([^<]+)</h[456]>", RegexOptions.IgnoreCase)]
    private static partial Regex H456TagRegex();

    [GeneratedRegex(@"<li[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LiTagRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrTagRegex();

    [GeneratedRegex(@"<p[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex PTagRegex();

    [GeneratedRegex(@"<strong[^>]*>([^<]+)</strong>", RegexOptions.IgnoreCase)]
    private static partial Regex StrongTagRegex();

    [GeneratedRegex(@"<b[^>]*>([^<]+)</b>", RegexOptions.IgnoreCase)]
    private static partial Regex BoldTagRegex();

    [GeneratedRegex(@"<em[^>]*>([^<]+)</em>", RegexOptions.IgnoreCase)]
    private static partial Regex EmTagRegex();

    [GeneratedRegex(@"<i[^>]*>([^<]+)</i>", RegexOptions.IgnoreCase)]
    private static partial Regex ItalicTagRegex();

    [GeneratedRegex(@"<code[^>]*>([^<]+)</code>", RegexOptions.IgnoreCase)]
    private static partial Regex CodeTagRegex();

    [GeneratedRegex(@"<blockquote[^>]*>([^<]+)</blockquote>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockquoteTagRegex();

    // Tag stripping and cleanup
    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex AnyHtmlTagRegex();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex HorizontalWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveNewlinesRegex();

    // Link extraction
    [GeneratedRegex(@"<a[^>]*href=[""']([^""']+)[""'][^>]*>([^<]*)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorTagRegex();
}
