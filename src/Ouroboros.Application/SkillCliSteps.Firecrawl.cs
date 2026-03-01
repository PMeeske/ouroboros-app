// ==========================================================
// Skill CLI Steps - Firecrawl Web Scraping Integration
// ==========================================================

using System.Text;
using System.Text.Json;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Application.Json;

namespace Ouroboros.Application;

public static partial class SkillCliSteps
{
    // ========================================================================
    // FIRECRAWL WEB SCRAPING
    // ========================================================================

    /// <summary>
    /// Scrape a webpage using Firecrawl API for clean, structured content extraction.
    /// Requires FIRECRAWL_API_KEY environment variable.
    /// Usage: Firecrawl 'https://example.com' | UseOutput
    /// </summary>
    [PipelineToken("Firecrawl", "FirecrawlScrape", "Scrape", "WebScrape")]
    public static Step<CliPipelineState, CliPipelineState> Firecrawl(string? url = null)
        => async s =>
        {
            string targetUrl = ParseString(url) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                Console.WriteLine("[Firecrawl] No URL provided");
                return s;
            }

            string? apiKey = Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("[Firecrawl] ⚠ FIRECRAWL_API_KEY not set, falling back to basic Fetch");
                return await Fetch(targetUrl)(s);
            }

            // Fix malformed URLs
            targetUrl = FixMalformedUrl(targetUrl);

            if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out Uri? parsedUri))
            {
                Console.WriteLine($"[Firecrawl] ⚠ Invalid URL: {targetUrl}");
                s.Output = "Firecrawl failed: Invalid URL format";
                return s;
            }

            Console.WriteLine($"[Firecrawl] Scraping: {targetUrl}");
            try
            {
                var result = await FirecrawlScrapeAsync(targetUrl, apiKey);
                Console.WriteLine($"[Firecrawl] ✓ Retrieved {result.Length:N0} characters");
                s.Output = result.Length > 50000 ? result[..50000] + "\n...[truncated]" : result;
                s.Context = $"[Firecrawl scraped from {targetUrl}]\n{s.Output}";
            }
            catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
            {
                Console.WriteLine($"[Firecrawl] ⚠ Failed: {ex.Message}, falling back to basic Fetch");
                return await Fetch(targetUrl)(s);
            }
            return s;
        };

    /// <summary>
    /// Scrape a webpage and extract specific content using Firecrawl with LLM extraction.
    /// Requires FIRECRAWL_API_KEY environment variable.
    /// Usage: FirecrawlExtract 'https://example.com' 'Extract product prices and descriptions' | UseOutput
    /// </summary>
    [PipelineToken("FirecrawlExtract", "ExtractFromWeb", "SmartScrape")]
    public static Step<CliPipelineState, CliPipelineState> FirecrawlExtract(string? url = null, string? extractPrompt = null)
        => async s =>
        {
            string targetUrl = ParseString(url) ?? s.Prompt ?? s.Query;
            string prompt = ParseString(extractPrompt) ?? "Extract the main content and key information";

            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                Console.WriteLine("[FirecrawlExtract] No URL provided");
                return s;
            }

            string? apiKey = Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("[FirecrawlExtract] ⚠ FIRECRAWL_API_KEY not set");
                s.Output = "FirecrawlExtract requires FIRECRAWL_API_KEY environment variable";
                return s;
            }

            targetUrl = FixMalformedUrl(targetUrl);

            Console.WriteLine($"[FirecrawlExtract] Scraping: {targetUrl}");
            Console.WriteLine($"[FirecrawlExtract] Extraction prompt: {prompt}");
            try
            {
                var result = await FirecrawlScrapeAsync(targetUrl, apiKey, extractionPrompt: prompt);
                Console.WriteLine($"[FirecrawlExtract] ✓ Extracted {result.Length:N0} characters");
                s.Output = result;
                s.Context = $"[Firecrawl extracted from {targetUrl}]\n{s.Output}";
            }
            catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
            {
                Console.WriteLine($"[FirecrawlExtract] ⚠ Failed: {ex.Message}");
                s.Output = $"FirecrawlExtract failed: {ex.Message}";
            }
            return s;
        };

    /// <summary>
    /// Crawl multiple pages from a website starting from a URL using Firecrawl.
    /// Requires FIRECRAWL_API_KEY environment variable.
    /// Usage: FirecrawlCrawl 'https://example.com' 5 | UseOutput
    /// </summary>
    [PipelineToken("FirecrawlCrawl", "CrawlSite", "WebCrawl")]
    public static Step<CliPipelineState, CliPipelineState> FirecrawlCrawl(string? url = null, int maxPages = 5)
        => async s =>
        {
            string targetUrl = ParseString(url) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                Console.WriteLine("[FirecrawlCrawl] No URL provided");
                return s;
            }

            string? apiKey = Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("[FirecrawlCrawl] ⚠ FIRECRAWL_API_KEY not set");
                s.Output = "FirecrawlCrawl requires FIRECRAWL_API_KEY environment variable";
                return s;
            }

            targetUrl = FixMalformedUrl(targetUrl);
            maxPages = Math.Clamp(maxPages, 1, 50);

            Console.WriteLine($"[FirecrawlCrawl] Starting crawl: {targetUrl} (max {maxPages} pages)");
            try
            {
                var result = await FirecrawlCrawlAsync(targetUrl, apiKey, maxPages);
                Console.WriteLine($"[FirecrawlCrawl] ✓ Crawled content: {result.Length:N0} characters");
                s.Output = result.Length > 100000 ? result[..100000] + "\n...[truncated]" : result;
                s.Context = $"[Firecrawl crawled from {targetUrl}]\n{s.Output}";
            }
            catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
            {
                Console.WriteLine($"[FirecrawlCrawl] ⚠ Failed: {ex.Message}");
                s.Output = $"FirecrawlCrawl failed: {ex.Message}";
            }
            return s;
        };

    /// <summary>
    /// Scrape a page using Firecrawl API and return markdown content.
    /// </summary>
    private static async Task<string> FirecrawlScrapeAsync(string url, string apiKey, string? extractionPrompt = null)
    {
        var requestBody = new Dictionary<string, object>
        {
            ["url"] = url,
            ["formats"] = new[] { "markdown" },
            ["onlyMainContent"] = true,
            ["blockAds"] = true,
            ["removeBase64Images"] = true
        };

        if (!string.IsNullOrEmpty(extractionPrompt))
        {
            requestBody["formats"] = new object[] { "markdown", new Dictionary<string, object> { ["type"] = "json", ["prompt"] = extractionPrompt } };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.firecrawl.dev/v2/scrape");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

        var response = await _httpClient.Value.SendAsync(request);
        string json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Firecrawl API error: {response.StatusCode} - {json}");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("success", out var successEl) || !successEl.GetBoolean())
        {
            string error = root.TryGetProperty("error", out var errEl) ? errEl.GetString() ?? "Unknown error" : "Request failed";
            throw new InvalidOperationException($"Firecrawl scrape failed: {error}");
        }

        var data = root.GetProperty("data");
        var result = new StringBuilder();

        // Get metadata
        if (data.TryGetProperty("metadata", out var metadata))
        {
            if (metadata.TryGetProperty("title", out var titleEl))
                result.AppendLine($"# {titleEl.GetString()}");
            if (metadata.TryGetProperty("description", out var descEl))
                result.AppendLine($"\n> {descEl.GetString()}\n");
        }

        // Get markdown content
        if (data.TryGetProperty("markdown", out var markdownEl))
        {
            result.AppendLine(markdownEl.GetString());
        }

        // Get extracted JSON if available
        if (data.TryGetProperty("json", out var jsonEl))
        {
            result.AppendLine("\n## Extracted Data");
            result.AppendLine("```json");
            result.AppendLine(JsonSerializer.Serialize(jsonEl, JsonDefaults.IndentedExact));
            result.AppendLine("```");
        }

        return result.ToString();
    }

    /// <summary>
    /// Crawl multiple pages from a website using Firecrawl crawl endpoint.
    /// </summary>
    private static async Task<string> FirecrawlCrawlAsync(string url, string apiKey, int maxPages)
    {
        var requestBody = new Dictionary<string, object>
        {
            ["url"] = url,
            ["limit"] = maxPages,
            ["scrapeOptions"] = new Dictionary<string, object>
            {
                ["formats"] = new[] { "markdown" },
                ["onlyMainContent"] = true
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.firecrawl.dev/v2/crawl");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

        var response = await _httpClient.Value.SendAsync(request);
        string json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Firecrawl API error: {response.StatusCode} - {json}");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Crawl endpoint returns a job ID for async crawling
        if (root.TryGetProperty("id", out var jobIdEl))
        {
            string jobId = jobIdEl.GetString() ?? throw new InvalidOperationException("No job ID returned");
            Console.WriteLine($"[FirecrawlCrawl] Job started: {jobId}");

            // Poll for results
            return await PollFirecrawlCrawlJobAsync(jobId, apiKey);
        }

        // Direct results (for small crawls)
        if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
        {
            return BuildCrawlResultsMarkdown(dataEl);
        }

        throw new InvalidOperationException("Unexpected Firecrawl response format");
    }

    /// <summary>
    /// Poll Firecrawl crawl job until completion.
    /// </summary>
    private static async Task<string> PollFirecrawlCrawlJobAsync(string jobId, string apiKey)
    {
        int maxAttempts = 60; // 5 minutes max
        for (int i = 0; i < maxAttempts; i++)
        {
            await Task.Delay(5000); // Poll every 5 seconds

            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.firecrawl.dev/v2/crawl/{jobId}");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.Value.SendAsync(request);
            string json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var statusEl))
            {
                string status = statusEl.GetString() ?? "";
                if (status == "completed")
                {
                    if (root.TryGetProperty("data", out var dataEl))
                    {
                        return BuildCrawlResultsMarkdown(dataEl);
                    }
                }
                else if (status == "failed")
                {
                    string error = root.TryGetProperty("error", out var errEl) ? errEl.GetString() ?? "Unknown" : "Unknown";
                    throw new InvalidOperationException($"Crawl job failed: {error}");
                }

                Console.WriteLine($"[FirecrawlCrawl] Status: {status}...");
            }
        }

        throw new TimeoutException("Firecrawl crawl job timed out");
    }

    /// <summary>
    /// Build markdown output from crawl results array.
    /// </summary>
    private static string BuildCrawlResultsMarkdown(JsonElement dataArray)
    {
        var result = new StringBuilder();
        int pageNum = 0;

        foreach (var page in dataArray.EnumerateArray())
        {
            pageNum++;
            result.AppendLine($"---\n## Page {pageNum}");

            if (page.TryGetProperty("metadata", out var metadata))
            {
                if (metadata.TryGetProperty("title", out var titleEl))
                    result.AppendLine($"### {titleEl.GetString()}");
                if (metadata.TryGetProperty("sourceURL", out var urlEl))
                    result.AppendLine($"URL: {urlEl.GetString()}");
            }

            if (page.TryGetProperty("markdown", out var markdownEl))
            {
                result.AppendLine();
                result.AppendLine(markdownEl.GetString());
            }

            result.AppendLine();
        }

        return result.ToString();
    }
}
