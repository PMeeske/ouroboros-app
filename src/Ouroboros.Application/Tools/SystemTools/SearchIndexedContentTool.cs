// <copyright file="SearchIndexedContentTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;
using System.Text.Json;
using Ouroboros.Application.Services;

/// <summary>
/// Search indexed content semantically.
/// </summary>
internal class SearchIndexedContentTool : ITool
{
    public string Name => "search_indexed";
    public string Description => "Search previously indexed files semantically. Input: JSON {\"query\":\"...\", \"limit\":5} or just a search query.";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (SystemAccessTools.SharedIndexer == null)
        {
            return Result<string, string>.Failure("Self-indexer not available. Qdrant may not be connected.");
        }

        try
        {
            string query;
            int limit = 5;

            // Try to parse as JSON first
            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(input);
                query = args.GetProperty("query").GetString() ?? "";
                if (args.TryGetProperty("limit", out var limEl))
                    limit = limEl.GetInt32();
            }
            catch
            {
                // Plain text query
                query = input.Trim();
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return Result<string, string>.Failure("Query cannot be empty");
            }

            var results = await SystemAccessTools.SharedIndexer.SearchAsync(query, limit, scoreThreshold: 0.3f, ct);

            // Record access patterns for knowledge reorganization
            SystemAccessTools.SharedIndexer.RecordAccess(results);

            if (results.Count == 0)
            {
                return Result<string, string>.Success("No matching content found in indexed files.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {results.Count} relevant matches:\n");

            foreach (var result in results)
            {
                sb.AppendLine($"  {result.FilePath} (chunk {result.ChunkIndex + 1}, score: {result.Score:F2})");
                var preview = result.Content.Length > 200
                    ? result.Content.Substring(0, 200) + "..."
                    : result.Content;
                sb.AppendLine($"   {preview.Replace("\n", " ").Replace("\r", "")}\n");
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Search failed: {ex.Message}");
        }
    }
}
