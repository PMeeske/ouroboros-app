// <copyright file="DynamicToolFactory.ContentParsing.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// HTML parsing and content extraction for DynamicToolFactory.
/// </summary>
public partial class DynamicToolFactory
{
    /// <summary>
    /// Detects if content appears to be binary/compressed rather than text.
    /// </summary>
    private static bool IsBinaryContent(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;

        // Check first 1000 chars for binary indicators
        int checkLength = Math.Min(content.Length, 1000);
        int nonPrintable = 0;

        for (int i = 0; i < checkLength; i++)
        {
            char c = content[i];
            // Count non-printable chars (excluding common whitespace)
            if (c < 32 && c != '\t' && c != '\n' && c != '\r')
                nonPrintable++;
            // High rate of replacement chars indicates encoding issues
            if (c == '\uFFFD')
                nonPrintable++;
        }

        // If more than 10% is non-printable, likely binary
        return nonPrintable > checkLength * 0.1;
    }

    /// <summary>
    /// Sanitizes content for safe storage/embedding.
    /// </summary>
    private static string SanitizeForStorage(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;

        var sb = new System.Text.StringBuilder(content.Length);
        foreach (char c in content)
        {
            // Keep printable ASCII and common Unicode
            if (c >= 32 && c < 127) // Basic ASCII
                sb.Append(c);
            else if (c == '\t' || c == '\n' || c == '\r') // Whitespace
                sb.Append(c);
            else if (c >= 160 && c < 0xFFFD) // Extended Unicode (but not replacement char)
                sb.Append(c);
            else
                sb.Append(' '); // Replace problematic chars with space
        }

        return sb.ToString();
    }

    private static string ExtractCode(string response)
    {
        // Extract code from markdown code blocks if present
        var match = MarkdownCodeBlockRegex().Match(response);

        return match.Success ? match.Groups[1].Value.Trim() : response.Trim();
    }

    [GeneratedRegex(@"```(?:csharp|cs)?\s*([\s\S]*?)```", RegexOptions.Singleline)]
    private static partial Regex MarkdownCodeBlockRegex();

    /// <summary>
    /// Ensures that required using statements are present in the generated code.
    /// </summary>
    private static string EnsureRequiredUsings(string code)
    {
        var requiredUsings = new[]
        {
            "using System;",
            "using System.Threading;",
            "using System.Threading.Tasks;",
            "using Ouroboros.Core.Monads;",
            "using Ouroboros.Tools;",
        };

        var sb = new StringBuilder();
        var existingUsings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract existing using statements
        var lines = code.Split('\n');
        var usingLines = new List<string>();
        var codeStart = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("using ") && trimmed.EndsWith(";"))
            {
                existingUsings.Add(trimmed);
                usingLines.Add(lines[i]);
                codeStart = i + 1;
            }
            else if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("//"))
            {
                // Found non-using, non-empty line
                break;
            }
            else
            {
                codeStart = i + 1;
            }
        }

        // Add all existing usings
        foreach (var u in usingLines)
        {
            sb.AppendLine(u);
        }

        // Add missing required usings
        foreach (var required in requiredUsings)
        {
            if (!existingUsings.Contains(required))
            {
                sb.AppendLine(required);
            }
        }

        // Add the rest of the code
        if (usingLines.Count > 0)
        {
            sb.AppendLine();
        }

        for (int i = codeStart; i < lines.Length; i++)
        {
            sb.AppendLine(lines[i]);
        }

        return sb.ToString().TrimEnd();
    }

    private static List<string> ExtractSearchResults(string html, string provider)
    {
        var results = new List<string>();

        // Try multiple patterns for each provider (they change HTML frequently)
        var patterns = provider.ToLowerInvariant() switch
        {
            "google" => GoogleSearchPatterns,
            "bing" => BingSearchPatterns,
            "brave" => BraveSearchPatterns,
            _ => DuckDuckGoSearchPatterns
        };

        foreach (var regex in patterns)
        {
            var matches = regex.Matches(html);
            foreach (Match m in matches)
            {
                // Try all capture groups
                for (int i = 1; i <= m.Groups.Count - 1; i++)
                {
                    if (m.Groups[i].Success)
                    {
                        string text = System.Net.WebUtility.HtmlDecode(m.Groups[i].Value).Trim();
                        // Filter out noise
                        if (text.Length > 25 &&
                            !results.Contains(text) &&
                            !text.StartsWith("http") &&
                            !text.Contains("javascript:") &&
                            !text.All(c => char.IsDigit(c) || char.IsWhiteSpace(c)))
                        {
                            results.Add(text);
                        }
                    }
                }
            }

            // If we found results with this pattern, don't need to try more
            if (results.Count >= 3) break;
        }

        // Fallback: extract any reasonably sized text blocks if no results found
        if (results.Count == 0)
        {
            var fallbackMatches = FallbackTextBlockRegex().Matches(html);
            foreach (Match m in fallbackMatches)
            {
                string text = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
                if (text.Length > 40 &&
                    !results.Contains(text) &&
                    !text.StartsWith("http") &&
                    !text.Contains("{") &&  // Skip JSON/CSS
                    !text.Contains("function") &&  // Skip JS
                    !text.Contains("var ") && // Skip JS vars
                    !text.Contains("window.") && // Skip JS window objects
                    !text.Contains(";") && // Skip code-like lines
                    results.Count < 10)
                {
                    // Check for high density of symbols (likely code or garbage)
                    int symbols = text.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && c != '.' && c != ',');
                    if (symbols < text.Length * 0.2) // Less than 20% symbols
                    {
                        results.Add(text);
                    }
                }
            }
        }

        return results;
    }

    // Pre-compiled search result regex patterns

    private static readonly Regex[] GoogleSearchPatterns =
    [
        GoogleSpanRegex(),
        GoogleBNeaweRegex(),
        GoogleSncfRegex(),
    ];

    [GeneratedRegex(@"<span[^>]*>([^<]{50,})</span>", RegexOptions.IgnoreCase)]
    private static partial Regex GoogleSpanRegex();

    [GeneratedRegex(@"<div[^>]*class=""[^""]*BNeawe[^""]*""[^>]*>([^<]{30,})</div>", RegexOptions.IgnoreCase)]
    private static partial Regex GoogleBNeaweRegex();

    [GeneratedRegex(@"<div[^>]*data-sncf=""[^""]*""[^>]*>([^<]{30,})</div>", RegexOptions.IgnoreCase)]
    private static partial Regex GoogleSncfRegex();

    private static readonly Regex[] BingSearchPatterns =
    [
        BingParagraphRegex(),
        BingAlgoRegex(),
        BingAlgoSlugRegex(),
    ];

    [GeneratedRegex(@"<p[^>]*>([^<]{50,})</p>", RegexOptions.IgnoreCase)]
    private static partial Regex BingParagraphRegex();

    [GeneratedRegex(@"<li[^>]*class=""b_algo""[^>]*>.*?<p>([^<]{30,})</p>", RegexOptions.IgnoreCase)]
    private static partial Regex BingAlgoRegex();

    [GeneratedRegex(@"<span[^>]*class=""algoSlug_icon""[^>]*>([^<]{30,})</span>", RegexOptions.IgnoreCase)]
    private static partial Regex BingAlgoSlugRegex();

    private static readonly Regex[] BraveSearchPatterns =
    [
        BraveSnippetPRegex(),
        BraveSnippetDivRegex(),
        BraveDescSpanRegex(),
        BraveResultItemRegex(),
    ];

    [GeneratedRegex(@"<p[^>]*class=""[^""]*snippet[^""]*""[^>]*>([^<]{30,})</p>", RegexOptions.IgnoreCase)]
    private static partial Regex BraveSnippetPRegex();

    [GeneratedRegex(@"<div[^>]*class=""[^""]*snippet[^""]*""[^>]*>([^<]{30,})</div>", RegexOptions.IgnoreCase)]
    private static partial Regex BraveSnippetDivRegex();

    [GeneratedRegex(@"<span[^>]*class=""[^""]*description[^""]*""[^>]*>([^<]{30,})</span>", RegexOptions.IgnoreCase)]
    private static partial Regex BraveDescSpanRegex();

    [GeneratedRegex(@"data-testid=""result-item""[^>]*>.*?<p[^>]*>([^<]{30,})</p>", RegexOptions.IgnoreCase)]
    private static partial Regex BraveResultItemRegex();

    private static readonly Regex[] DuckDuckGoSearchPatterns =
    [
        DdgResultSnippetARegex(),
        DdgResultSnippetTdRegex(),
        DdgResultSnippetClassRegex(),
        DdgResultARegex(),
        DdgSnippetDivRegex(),
        DdgSnippetSpanRegex(),
        DdgLiteTdRegex(),
        DdgLiteTrTdRegex(),
    ];

    [GeneratedRegex(@"<a[^>]*class=""result__snippet""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex DdgResultSnippetARegex();

    [GeneratedRegex(@"<td[^>]*class=""result__snippet""[^>]*>([^<]+)</td>", RegexOptions.IgnoreCase)]
    private static partial Regex DdgResultSnippetTdRegex();

    [GeneratedRegex(@"class=""result__snippet""[^>]*>([^<]{20,})<", RegexOptions.IgnoreCase)]
    private static partial Regex DdgResultSnippetClassRegex();

    [GeneratedRegex(@"<a[^>]*class=""[^""]*result[^""]*""[^>]*>([^<]{30,})</a>", RegexOptions.IgnoreCase)]
    private static partial Regex DdgResultARegex();

    [GeneratedRegex(@"<div[^>]*class=""[^""]*snippet[^""]*""[^>]*>([^<]{30,})</div>", RegexOptions.IgnoreCase)]
    private static partial Regex DdgSnippetDivRegex();

    [GeneratedRegex(@"<span[^>]*class=""[^""]*snippet[^""]*""[^>]*>([^<]{30,})</span>", RegexOptions.IgnoreCase)]
    private static partial Regex DdgSnippetSpanRegex();

    [GeneratedRegex(@"<td>([^<]{40,})</td>", RegexOptions.IgnoreCase)]
    private static partial Regex DdgLiteTdRegex();

    [GeneratedRegex(@"<tr[^>]*>.*?<td[^>]*>([^<]{30,})</td>", RegexOptions.IgnoreCase)]
    private static partial Regex DdgLiteTrTdRegex();

    [GeneratedRegex(@">([^<]{40,200})<")]
    private static partial Regex FallbackTextBlockRegex();
}
