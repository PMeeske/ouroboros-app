// <copyright file="FirecrawlScrapeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Scrapes a webpage using Firecrawl for clean content extraction.
/// </summary>
public class FirecrawlScrapeTool : ITool
{
    private static readonly HttpClient _sharedHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        AutomaticDecompression = System.Net.DecompressionMethods.All
    }) { Timeout = TimeSpan.FromSeconds(60) };

    /// <inheritdoc/>
    public string Name => "firecrawl_scrape";

    /// <inheritdoc/>
    public string Description => "Scrape a webpage using Firecrawl API for clean, structured content. Requires FIRECRAWL_API_KEY environment variable. Input: URL to scrape.";

    /// <inheritdoc/>
    public string? JsonSchema => """{"type":"object","properties":{"url":{"type":"string","description":"URL to scrape"}},"required":["url"]}""";

    /// <inheritdoc/>
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        string url = input.Trim();

        // Try to parse JSON input
        try
        {
            using var doc = JsonDocument.Parse(input);
            if (doc.RootElement.TryGetProperty("url", out var urlEl))
                url = urlEl.GetString() ?? url;
        }
        catch (System.Text.Json.JsonException) { /* Use raw input as URL */ }

        if (string.IsNullOrWhiteSpace(url))
            return Result<string, string>.Failure("No URL provided. Usage: firecrawl_scrape <url>");

        // Get API key from configuration or environment
        string? apiKey = ApiKeyProvider.GetApiKey("Firecrawl");
        if (string.IsNullOrWhiteSpace(apiKey))
            return Result<string, string>.Failure("Firecrawl API key not configured. Set ApiKeys:Firecrawl in user secrets or FIRECRAWL_API_KEY environment variable.");

        try
        {
            var result = await FirecrawlScrapeInternalAsync(url, apiKey, ct);
            return Result<string, string>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Firecrawl scrape failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"Firecrawl scrape failed: {ex.Message}");
        }
    }

    internal static async Task<string> FirecrawlScrapeInternalAsync(string url, string apiKey, CancellationToken ct)
    {
        var client = _sharedHttpClient;

        var requestBody = new Dictionary<string, object>
        {
            ["url"] = url,
            ["formats"] = new[] { "markdown" },
            ["onlyMainContent"] = true,
            ["blockAds"] = true,
            ["removeBase64Images"] = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.firecrawl.dev/v2/scrape");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request, ct);
        string json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Firecrawl API error: {response.StatusCode}");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("success", out var successEl) || !successEl.GetBoolean())
            throw new InvalidOperationException("Firecrawl request failed");

        var data = root.GetProperty("data");
        var result = new StringBuilder();

        if (data.TryGetProperty("metadata", out var metadata))
        {
            if (metadata.TryGetProperty("title", out var titleEl))
                result.AppendLine($"# {titleEl.GetString()}");
            if (metadata.TryGetProperty("sourceURL", out var srcEl))
                result.AppendLine($"Source: {srcEl.GetString()}");
        }

        if (data.TryGetProperty("markdown", out var markdownEl))
        {
            result.AppendLine();
            var content = markdownEl.GetString() ?? "";
            result.AppendLine(content.Length > 30000 ? content[..30000] + "\n...[truncated]" : content);
        }

        return result.ToString();
    }
}
