#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Skill CLI Steps - Research-powered skills as DSL tokens
// Dynamic web fetching + full pipeline awareness
// ==========================================================

using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Application.Json;

namespace Ouroboros.Application;

/// <summary>
/// CLI steps for research-powered skills that chain with other DSL tokens.
/// These steps integrate the skill registry with the standard pipeline.
/// Includes dynamic web fetching from arXiv, Wikipedia, Semantic Scholar, and any URL.
/// </summary>
public static partial class SkillCliSteps
{
    // Shared HTTP client for web fetching
    private static readonly Lazy<HttpClient> _httpClient = new(() => new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "Ouroboros/1.0 (Research Pipeline)" } }
    });

    // Shared skill registry across the pipeline
    private static readonly Lazy<SkillRegistry> _registry = new(() =>
    {
        var registry = new SkillRegistry();
        RegisterPredefinedSkills(registry);
        return registry;
    });

    // Dynamic discovery of ALL pipeline tokens at runtime
    private static readonly Lazy<Dictionary<string, PipelineTokenInfo>> _allPipelineTokens = new(() =>
    {
        var tokens = new Dictionary<string, PipelineTokenInfo>(StringComparer.OrdinalIgnoreCase);

        // Scan all loaded assemblies for PipelineToken attributes
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        var attr = method.GetCustomAttribute<PipelineTokenAttribute>();
                        if (attr != null)
                        {
                            var xmlDoc = method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description
                                ?? ExtractXmlDocSummary(method);

                            var info = new PipelineTokenInfo(
                                attr.Names.FirstOrDefault() ?? method.Name,
                                attr.Names.Skip(1).ToArray(),
                                type.Name,
                                xmlDoc ?? $"Pipeline step from {type.Name}",
                                method
                            );

                            foreach (var name in attr.Names)
                            {
                                tokens[name] = info;
                            }
                        }
                    }
                }
            }
            catch (Exception) { /* Skip assemblies that can't be scanned */ }
        }

        return tokens;
    });

    /// <summary>
    /// Get all discovered pipeline tokens for LLM context.
    /// </summary>
    public static IReadOnlyDictionary<string, PipelineTokenInfo> GetAllPipelineTokens() => _allPipelineTokens.Value;

    /// <summary>
    /// Build a comprehensive context string of all pipeline capabilities for the LLM.
    /// </summary>
    public static string BuildPipelineContext()
    {
        var tokens = _allPipelineTokens.Value;
        var grouped = tokens.Values.Distinct().GroupBy(t => t.SourceClass);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("OUROBOROS PIPELINE - ALL AVAILABLE DSL TOKENS:");
        sb.AppendLine("================================================");

        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            sb.AppendLine($"\n📦 {group.Key}:");
            foreach (var token in group.Take(10)) // Limit per group
            {
                string aliases = token.Aliases.Length > 0 ? $" (aliases: {string.Join(", ", token.Aliases.Take(2))})" : "";
                sb.AppendLine($"  • {token.PrimaryName}{aliases}");
                if (!string.IsNullOrEmpty(token.Description) && token.Description.Length < 80)
                    sb.AppendLine($"    {token.Description}");
            }
        }

        sb.AppendLine($"\nTotal: {tokens.Values.Distinct().Count()} pipeline tokens available");
        return sb.ToString();
    }

    // === Shared Helpers ===

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        Match m = ParseSingleQuotedStringRegex().Match(arg);
        if (m.Success) return m.Groups["s"].Value;
        m = ParseDoubleQuotedStringRegex().Match(arg);
        if (m.Success) return m.Groups["s"].Value;
        return arg;
    }

    [GeneratedRegex(@"^'(?<s>.*)'$", RegexOptions.Singleline)]
    private static partial Regex ParseSingleQuotedStringRegex();

    [GeneratedRegex(@"^""(?<s>.*)""$", RegexOptions.Singleline)]
    private static partial Regex ParseDoubleQuotedStringRegex();

    /// <summary>
    /// Fixes malformed URLs that LLMs sometimes generate (e.g., "https: example.com path" -> "https://example.com/path").
    /// </summary>
    private static string FixMalformedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        url = url.Trim();

        // Fix "https: " -> "https://" and "http: " -> "http://"
        url = MalformedSchemeRegex().Replace(url, "$1://");

        // If URL still doesn't have proper scheme, check for common patterns
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            // Check if it looks like a domain (e.g., "www.example.com" or "example.com")
            if (DomainLikeRegex().IsMatch(url))
            {
                url = "https://" + url;
            }
        }

        // Try to parse and fix path components with spaces
        if (Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            // URL is valid, return as-is
            return url;
        }

        // If parsing failed, try to fix spaces in path
        // Pattern: "https://domain path1 path2" -> "https://domain/path1/path2"
        var match = DomainWithPathRegex().Match(url);
        if (match.Success)
        {
            string domain = match.Groups[1].Value;
            string path = match.Groups[2].Value.Trim();

            // Replace spaces with / in path, normalize multiple slashes
            path = WhitespaceRegex().Replace(path, "/");
            path = MultipleSlashRegex().Replace(path, "/");

            if (!path.StartsWith("/"))
                path = "/" + path;

            url = domain + path;
        }

        return url;
    }

    [GeneratedRegex(@"^(https?): +")]
    private static partial Regex MalformedSchemeRegex();

    [GeneratedRegex(@"^[\w\-]+(\.[\w\-]+)+")]
    private static partial Regex DomainLikeRegex();

    [GeneratedRegex(@"^(https?://[\w\.\-]+)([\s/].*)$")]
    private static partial Regex DomainWithPathRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"/+")]
    private static partial Regex MultipleSlashRegex();

    /// <summary>
    /// Clone a pipeline state for parallel execution without side effects.
    /// </summary>
    private static CliPipelineState CloneState(CliPipelineState s) => new()
    {
        Branch = s.Branch,
        Llm = s.Llm,
        Tools = s.Tools,
        Embed = s.Embed,
        Topic = s.Topic,
        Query = s.Query,
        Prompt = s.Prompt,
        RetrievalK = s.RetrievalK,
        Trace = s.Trace,
        Context = s.Context,
        Output = s.Output,
        MeTTaEngine = s.MeTTaEngine,
        VectorStore = s.VectorStore,
        Streaming = s.Streaming,
        ActiveStream = s.ActiveStream
    };

    /// <summary>
    /// Extract text content from HTML, removing all tags.
    /// </summary>
    private static string ExtractTextFromHtml(string html)
    {
        // Remove script and style elements
        html = ScriptTagRegex().Replace(html, "");
        html = StyleTagRegex().Replace(html, "");

        // Remove HTML tags
        html = HtmlTagRegex().Replace(html, " ");

        // Decode HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);

        // Collapse whitespace
        html = WhitespaceRegex().Replace(html, " ").Trim();

        return html;
    }

    [GeneratedRegex(@"<script[^>]*>[\s\S]*?</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTagRegex();

    [GeneratedRegex(@"<style[^>]*>[\s\S]*?</style>", RegexOptions.IgnoreCase)]
    private static partial Regex StyleTagRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    /// <summary>
    /// Attempt to extract XML documentation summary from a method (best effort).
    /// </summary>
    private static string? ExtractXmlDocSummary(MethodInfo method)
    {
        // Try to get from XML documentation attribute or fallback to method name
        var docAttr = method.GetCustomAttributes()
            .FirstOrDefault(a => a.GetType().Name.Contains("Documentation") || a.GetType().Name.Contains("Summary"));

        if (docAttr != null)
        {
            var descProp = docAttr.GetType().GetProperty("Description") ?? docAttr.GetType().GetProperty("Summary");
            if (descProp != null)
                return descProp.GetValue(docAttr)?.ToString();
        }

        // Fallback: generate description from method name
        var name = method.Name;
        // Insert spaces before capitals: "MyMethodName" -> "My Method Name"
        var spaced = PascalCaseSplitRegex().Replace(name, " $1");
        return $"Pipeline step: {spaced}";
    }

    [GeneratedRegex(@"(?<!^)([A-Z])")]
    private static partial Regex PascalCaseSplitRegex();
}
