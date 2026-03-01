// ==========================================================
// Skill CLI Steps - Web Search (Scholar, Google/DuckDuckGo, GitHub, News)
// ==========================================================

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Ouroboros.Application;

public static partial class SkillCliSteps
{
    /// <summary>
    /// Search Semantic Scholar for academic papers with citations.
    /// Usage: ScholarSearch 'deep learning' | UseOutput
    /// </summary>
    [PipelineToken("ScholarSearch", "SemanticScholar", "Scholar", "AcademicSearch")]
    public static Step<CliPipelineState, CliPipelineState> ScholarSearch(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[ScholarSearch] No query provided");
                return s;
            }

            Console.WriteLine($"[ScholarSearch] Searching: \"{searchQuery}\"");
            string url = $"https://api.semanticscholar.org/graph/v1/paper/search?query={Uri.EscapeDataString(searchQuery)}&limit=10&fields=title,authors,year,citationCount,abstract";

            try
            {
                string json = await _httpClient.Value.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);

                var results = new List<string>();
                if (doc.RootElement.TryGetProperty("data", out var dataEl))
                {
                    foreach (var paper in dataEl.EnumerateArray().Take(10))
                    {
                        string title = paper.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "" : "";
                        int citations = paper.TryGetProperty("citationCount", out var c) ? c.GetInt32() : 0;
                        int year = paper.TryGetProperty("year", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32() : 0;
                        string abstractText = paper.TryGetProperty("abstract", out var a) ? a.GetString() ?? "" : "";

                        var authors = new List<string>();
                        if (paper.TryGetProperty("authors", out var authorsEl))
                        {
                            foreach (var author in authorsEl.EnumerateArray().Take(3))
                            {
                                if (author.TryGetProperty("name", out var name))
                                    authors.Add(name.GetString() ?? "");
                            }
                        }

                        results.Add($"[Paper] {title} ({year})\n   Authors: {string.Join(", ", authors)}\n   Citations: {citations:N0}\n   {(abstractText.Length > 150 ? abstractText[..150] + "..." : abstractText)}");
                    }
                }

                Console.WriteLine($"[ScholarSearch] Found {results.Count} papers");
                s.Output = string.Join("\n\n", results);
                s.Context = $"[Semantic Scholar: {searchQuery}]\n{s.Output}";
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[ScholarSearch] Failed: {ex.Message}");
                s.Output = $"Semantic Scholar search failed: {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"[ScholarSearch] Failed: {ex.Message}");
                s.Output = $"Semantic Scholar search failed: {ex.Message}";
            }
            return s;
        };

    /// <summary>
    /// Search the web using Google (via SerpAPI or scraping).
    /// Usage: GoogleSearch 'machine learning tutorials' | UseOutput
    /// </summary>
    [PipelineToken("GoogleSearch", "Google", "WebSearch", "SearchWeb")]
    public static Step<CliPipelineState, CliPipelineState> GoogleSearch(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[GoogleSearch] No query provided");
                return s;
            }

            Console.WriteLine($"[GoogleSearch] Searching: \"{searchQuery}\"");

            // Check for SerpAPI key in environment
            string? serpApiKey = Environment.GetEnvironmentVariable("SERPAPI_KEY");

            try
            {
                List<string> results;

                if (!string.IsNullOrEmpty(serpApiKey))
                {
                    // Use SerpAPI for reliable Google results
                    results = await SearchWithSerpApiAsync(searchQuery, serpApiKey);
                }
                else
                {
                    // Fallback to DuckDuckGo HTML (more reliable than scraping Google)
                    results = await SearchWithDuckDuckGoAsync(searchQuery);
                }

                if (results.Count > 0)
                {
                    Console.WriteLine($"[GoogleSearch] Found {results.Count} results");
                    s.Output = string.Join("\n\n", results);
                    s.Context = $"[Web search: {searchQuery}]\n{s.Output}";
                }
                else
                {
                    Console.WriteLine("[GoogleSearch] No results found");
                    s.Output = "No search results found.";
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[GoogleSearch] Failed: {ex.Message}");
                s.Output = $"Web search failed: {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"[GoogleSearch] Failed: {ex.Message}");
                s.Output = $"Web search failed: {ex.Message}";
            }
            return s;
        };

    private static async Task<List<string>> SearchWithSerpApiAsync(string query, string apiKey)
    {
        string url = $"https://serpapi.com/search.json?q={Uri.EscapeDataString(query)}&api_key={apiKey}&num=10";
        string json = await _httpClient.Value.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);

        var results = new List<string>();
        if (doc.RootElement.TryGetProperty("organic_results", out var organicEl))
        {
            foreach (var result in organicEl.EnumerateArray().Take(10))
            {
                string title = result.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "" : "";
                string link = result.TryGetProperty("link", out var l) ? l.GetString() ?? "" : "";
                string snippet = result.TryGetProperty("snippet", out var sProp) ? sProp.GetString() ?? "" : "";
                results.Add($"[Result] {title}\n   {link}\n   {snippet}");
            }
        }
        return results;
    }

    private static async Task<List<string>> SearchWithDuckDuckGoAsync(string query)
    {
        string url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
        string html = await _httpClient.Value.GetStringAsync(url);

        var results = new List<string>();

        // Parse DuckDuckGo HTML results
        var resultMatches = DuckDuckGoResultRegex().Matches(html);

        foreach (Match match in resultMatches.Take(10))
        {
            string link = System.Net.WebUtility.UrlDecode(match.Groups[1].Value);
            // Extract actual URL from DuckDuckGo redirect
            var uddgMatch = DuckDuckGoUddgRegex().Match(link);
            if (uddgMatch.Success)
                link = System.Net.WebUtility.UrlDecode(uddgMatch.Groups[1].Value);

            string title = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value.Trim());
            string snippet = HtmlTagRegex().Replace(match.Groups[3].Value, "").Trim();
            snippet = System.Net.WebUtility.HtmlDecode(snippet);

            if (!string.IsNullOrEmpty(title))
                results.Add($"[Result] {title}\n   {link}\n   {snippet}");
        }

        // Fallback: simpler parsing if the above didn't work
        if (results.Count == 0)
        {
            var simpleMatches = DuckDuckGoSimpleResultRegex().Matches(html);
            foreach (Match match in simpleMatches.Take(10))
            {
                string title = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
                if (!string.IsNullOrEmpty(title) && title.Length > 10)
                    results.Add($"[Result] {title}");
            }
        }

        return results;
    }

    [GeneratedRegex(@"<a[^>]+class=""result__a""[^>]*href=""([^""]+)""[^>]*>([^<]+)</a>.*?<a[^>]+class=""result__snippet""[^>]*>([^<]*(?:<[^>]+>[^<]*)*)</a>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex DuckDuckGoResultRegex();

    [GeneratedRegex(@"uddg=([^&]+)")]
    private static partial Regex DuckDuckGoUddgRegex();

    [GeneratedRegex(@"<a[^>]+class=""[^""]*result[^""]*""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex DuckDuckGoSimpleResultRegex();

    /// <summary>
    /// Search GitHub for repositories.
    /// Usage: GithubSearch 'machine learning python' | UseOutput
    /// </summary>
    [PipelineToken("GithubSearch", "Github", "SearchGithub", "Repos")]
    public static Step<CliPipelineState, CliPipelineState> GithubSearch(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[GithubSearch] No query provided");
                return s;
            }

            Console.WriteLine($"[GithubSearch] Searching: \"{searchQuery}\"");
            string ghUrl = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(searchQuery)}&sort=stars&per_page=10";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, ghUrl);
                request.Headers.Add("Accept", "application/vnd.github.v3+json");

                var response = await _httpClient.Value.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var results = new List<string>();
                if (doc.RootElement.TryGetProperty("items", out var itemsEl))
                {
                    foreach (var repo in itemsEl.EnumerateArray().Take(10))
                    {
                        string name = repo.TryGetProperty("full_name", out var n) ? n.GetString() ?? "" : "";
                        string desc = repo.TryGetProperty("description", out var dProp) ? dProp.GetString() ?? "" : "";
                        int stars = repo.TryGetProperty("stargazers_count", out var st) ? st.GetInt32() : 0;
                        string lang = repo.TryGetProperty("language", out var l) ? l.GetString() ?? "" : "";
                        string repoUrl = repo.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";

                        results.Add($"[Repo] {name} ({stars:N0} stars)\n   Language: {lang}\n   {desc}\n   {repoUrl}");
                    }
                }

                Console.WriteLine($"[GithubSearch] Found {results.Count} repositories");
                s.Output = string.Join("\n\n", results);
                s.Context = $"[GitHub: {searchQuery}]\n{s.Output}";
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[GithubSearch] Failed: {ex.Message}");
                s.Output = $"GitHub search failed: {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"[GithubSearch] Failed: {ex.Message}");
                s.Output = $"GitHub search failed: {ex.Message}";
            }
            return s;
        };

    /// <summary>
    /// Search news via NewsAPI (requires NEWSAPI_KEY env var) or fallback to RSS.
    /// Usage: NewsSearch 'artificial intelligence' | UseOutput
    /// </summary>
    [PipelineToken("NewsSearch", "News", "SearchNews", "Headlines")]
    public static Step<CliPipelineState, CliPipelineState> NewsSearch(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[NewsSearch] No query provided");
                return s;
            }

            Console.WriteLine($"[NewsSearch] Searching: \"{searchQuery}\"");

            // Try Google News RSS as fallback (no API key needed)
            string newsUrl = $"https://news.google.com/rss/search?q={Uri.EscapeDataString(searchQuery)}&hl=en-US&gl=US&ceid=US:en";

            try
            {
                string xml = await _httpClient.Value.GetStringAsync(newsUrl);
                var doc = XDocument.Parse(xml);

                var results = new List<string>();
                foreach (var item in doc.Descendants("item").Take(10))
                {
                    string title = item.Element("title")?.Value ?? "";
                    string link = item.Element("link")?.Value ?? "";
                    string pubDate = item.Element("pubDate")?.Value ?? "";
                    string source = item.Element("source")?.Value ?? "";

                    results.Add($"[News] {title}\n   Source: {source} | {pubDate}\n   {link}");
                }

                Console.WriteLine($"[NewsSearch] Found {results.Count} articles");
                s.Output = string.Join("\n\n", results);
                s.Context = $"[News: {searchQuery}]\n{s.Output}";
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[NewsSearch] Failed: {ex.Message}");
                s.Output = $"News search failed: {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"[NewsSearch] Failed: {ex.Message}");
                s.Output = $"News search failed: {ex.Message}";
            }
            return s;
        };
}
