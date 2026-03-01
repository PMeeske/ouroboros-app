// <copyright file="SearchMyCodeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;
using Ouroboros.Application.Services;

/// <summary>
/// Search Ouroboros's own source code - enables self-introspection.
/// </summary>
internal class SearchMyCodeTool : ITool
{
    public string Name => "search_my_code";
    public string Description => "Search my own source code to understand how I work. I can introspect my implementation, find specific functions, or understand my architecture. Input: what to search for (e.g., 'how do I handle memory', 'consciousness implementation', 'tool registration').";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (SystemAccessTools.SharedIndexer == null)
        {
            return Result<string, string>.Failure("Self-indexer not available. I cannot access my own code right now.");
        }

        var query = input.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result<string, string>.Failure("Please specify what you want to know about my code.");
        }

        try
        {
            var results = await SystemAccessTools.SharedIndexer.SearchAsync(query, limit: 8, scoreThreshold: 0.25f, ct);

            // Record access patterns for knowledge reorganization
            SystemAccessTools.SharedIndexer.RecordAccess(results);

            if (results.Count == 0)
            {
                return Result<string, string>.Success($"I couldn't find code related to '{query}' in my indexed source.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {results.Count} relevant files for '{query}':\n");

            foreach (var result in results)
            {
                var relativePath = result.FilePath;
                try
                {
                    relativePath = Path.GetRelativePath(Environment.CurrentDirectory, result.FilePath);
                }
                catch
                {
                    // Path conversion failed - use absolute path as fallback
                }

                // Extract a brief summary (first meaningful line or truncated content)
                string summary = ExtractBriefSummary(result.Content, 80);
                sb.AppendLine($"* **{relativePath}** ({result.Score:P0}) - {summary}");
            }

            return Result<string, string>.Success(sb.ToString().Trim());
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Code introspection failed: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Result<string, string>.Failure($"Code introspection failed: {ex.Message}");
        }
    }

    private static string ExtractBriefSummary(string content, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "empty";

        // Find first non-empty, non-comment line
        var lines = content.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) &&
                       !l.StartsWith("//") &&
                       !l.StartsWith("/*") &&
                       !l.StartsWith("*") &&
                       !l.StartsWith("#") &&
                       !l.StartsWith("using ") &&
                       !l.StartsWith("namespace "));

        string firstLine = lines.FirstOrDefault() ?? content.Trim();

        if (firstLine.Length > maxLength)
            return firstLine[..maxLength] + "...";

        return firstLine;
    }
}
