#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Skill CLI Steps - Web Fetching (arXiv, Wikipedia, Scholar, Google, GitHub, News)
// ==========================================================

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Application;

public static partial class SkillCliSteps
{
    // ========================================================================
    // DYNAMIC WEB FETCHING STEPS
    // ========================================================================

    /// <summary>
    /// Fetch content from any URL dynamically.
    /// Usage: Fetch 'https://example.com/page' | UseOutput
    /// </summary>
    [PipelineToken("Fetch", "FetchUrl", "WebFetch", "HttpGet")]
    public static Step<CliPipelineState, CliPipelineState> Fetch(string? url = null)
        => async s =>
        {
            string targetUrl = ParseString(url) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                Console.WriteLine("[Fetch] No URL provided");
                return s;
            }

            // Fix malformed URLs from LLM (e.g., "https: example.com path" -> "https://example.com/path")
            targetUrl = FixMalformedUrl(targetUrl);

            // Validate URL is absolute
            if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out Uri? parsedUri))
            {
                Console.WriteLine($"[Fetch] ⚠ Invalid URL format: {targetUrl}");
                s.Output = $"Fetch failed: Invalid URL format. URL must be absolute (e.g., https://example.com)";
                return s;
            }

            // Only allow http/https schemes
            if (parsedUri.Scheme != "http" && parsedUri.Scheme != "https")
            {
                Console.WriteLine($"[Fetch] ⚠ Unsupported scheme: {parsedUri.Scheme}");
                s.Output = $"Fetch failed: Only http and https URLs are supported";
                return s;
            }

            Console.WriteLine($"[Fetch] Fetching: {targetUrl}");
            try
            {
                string content = await _httpClient.Value.GetStringAsync(parsedUri);

                // Extract text from HTML if needed
                if (content.Contains("<html") || content.Contains("<HTML"))
                {
                    content = ExtractTextFromHtml(content);
                }

                Console.WriteLine($"[Fetch] ✓ Retrieved {content.Length:N0} characters");
                s.Output = content.Length > 50000 ? content[..50000] + "\n...[truncated]" : content;
                s.Context = $"[Fetched from {targetUrl}]\n{s.Output}";
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[Fetch] ⚠ Failed: {ex.Message}");
                s.Output = $"Fetch failed: {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"[Fetch] ⚠ Failed: {ex.Message}");
                s.Output = $"Fetch failed: {ex.Message}";
            }
            return s;
        };

    /// <summary>
    /// Search arXiv for academic papers.
    /// Usage: ArxivSearch 'transformer attention' | UseOutput
    /// </summary>
    [PipelineToken("ArxivSearch", "SearchArxiv", "Arxiv", "Papers")]
    public static Step<CliPipelineState, CliPipelineState> ArxivSearch(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[ArxivSearch] No query provided");
                return s;
            }

            Console.WriteLine($"[ArxivSearch] Searching: \"{searchQuery}\"");
            string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(searchQuery)}&start=0&max_results=10";

            try
            {
                string xml = await _httpClient.Value.GetStringAsync(url);
                var doc = XDocument.Parse(xml);
                XNamespace atom = "http://www.w3.org/2005/Atom";

                var entries = doc.Descendants(atom + "entry").ToList();
                Console.WriteLine($"[ArxivSearch] ✓ Found {entries.Count} papers");

                var results = new List<string>();
                foreach (var entry in entries.Take(10))
                {
                    string title = entry.Element(atom + "title")?.Value?.Trim().Replace("\n", " ") ?? "Untitled";
                    string summary = entry.Element(atom + "summary")?.Value?.Trim().Replace("\n", " ") ?? "";
                    string id = entry.Element(atom + "id")?.Value ?? "";
                    string published = entry.Element(atom + "published")?.Value?[..10] ?? "";

                    var authors = entry.Descendants(atom + "author")
                        .Select(a => a.Element(atom + "name")?.Value)
                        .Where(n => n != null)
                        .Take(3);

                    results.Add($"📄 {title}\n   Authors: {string.Join(", ", authors)}\n   Published: {published}\n   ID: {id}\n   Summary: {(summary.Length > 200 ? summary[..200] + "..." : summary)}");
                }

                s.Output = string.Join("\n\n", results);
                s.Context = $"[arXiv search: {searchQuery}]\n{s.Output}";
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[ArxivSearch] ⚠ Failed: {ex.Message}");
                s.Output = $"arXiv search failed: {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"[ArxivSearch] ⚠ Failed: {ex.Message}");
                s.Output = $"arXiv search failed: {ex.Message}";
            }
            return s;
        };

    /// <summary>
    /// Search Wikipedia for information.
    /// Usage: WikiSearch 'neural networks' | UseOutput
    /// </summary>
    [PipelineToken("WikiSearch", "Wikipedia", "Wiki", "SearchWiki")]
    public static Step<CliPipelineState, CliPipelineState> WikiSearch(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[WikiSearch] No query provided");
                return s;
            }

            Console.WriteLine($"[WikiSearch] Searching: \"{searchQuery}\"");
            string url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(searchQuery.Replace(" ", "_"))}";

            try
            {
                string json = await _httpClient.Value.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string title = root.TryGetProperty("title", out var t) ? t.GetString() ?? searchQuery : searchQuery;
                string extract = root.TryGetProperty("extract", out var e) ? e.GetString() ?? "" : "";
                string description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

                Console.WriteLine($"[WikiSearch] ✓ Found: {title}");

                s.Output = $"📚 {title}\n{description}\n\n{extract}";
                s.Context = $"[Wikipedia: {title}]\n{s.Output}";
            }
            catch (HttpRequestException)
            {
                // Try search API instead
                Console.WriteLine($"[WikiSearch] Direct lookup failed, trying search...");
                string searchUrl = $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(searchQuery)}&format=json&srlimit=5";
                try
                {
                    string json = await _httpClient.Value.GetStringAsync(searchUrl);
                    using var doc = JsonDocument.Parse(json);

                    var results = new List<string>();
                    if (doc.RootElement.TryGetProperty("query", out var queryEl) &&
                        queryEl.TryGetProperty("search", out var searchEl))
                    {
                        foreach (var item in searchEl.EnumerateArray().Take(5))
                        {
                            string title = item.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? "" : "";
                            string snippet = item.TryGetProperty("snippet", out var snippetEl) ?
                                HtmlTagRegex().Replace(snippetEl.GetString() ?? "", "") : "";
                            results.Add($"📚 {title}\n   {snippet}");
                        }
                    }

                    s.Output = string.Join("\n\n", results);
                    s.Context = $"[Wikipedia search: {searchQuery}]\n{s.Output}";
                    Console.WriteLine($"[WikiSearch] ✓ Found {results.Count} results");
                }
                catch (HttpRequestException ex2)
                {
                    s.Output = $"Wikipedia search failed: {ex2.Message}";
                }
                catch (TaskCanceledException ex2)
                {
                    s.Output = $"Wikipedia search failed: {ex2.Message}";
                }
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"[WikiSearch] ⚠ Failed: {ex.Message}");
                s.Output = $"Wikipedia search failed: {ex.Message}";
            }
            return s;
        };

}
