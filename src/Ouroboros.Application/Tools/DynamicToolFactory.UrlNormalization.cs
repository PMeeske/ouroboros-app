// <copyright file="DynamicToolFactory.UrlNormalization.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Text.RegularExpressions;

/// <summary>
/// URL normalization and malformed URL fixing for DynamicToolFactory.
/// </summary>
public partial class DynamicToolFactory
{
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
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed))
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
            path = UrlPathWhitespaceRegex().Replace(path, "/");
            path = UrlPathMultipleSlashRegex().Replace(path, "/");

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
    private static partial Regex UrlPathWhitespaceRegex();

    [GeneratedRegex(@"/+")]
    private static partial Regex UrlPathMultipleSlashRegex();
}
