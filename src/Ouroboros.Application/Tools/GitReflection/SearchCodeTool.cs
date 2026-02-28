// <copyright file="SearchCodeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Searches code across the codebase.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Searches code across the codebase.
    /// </summary>
    public class SearchCodeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "search_my_codebase";

        /// <inheritdoc/>
        public string Description => "Search my own codebase for a pattern. Input JSON: {\"query\": \"search pattern\", \"regex\": false}. Returns matching lines with file and line number.";

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"query":{"type":"string"},"regex":{"type":"boolean"}},"required":["query"]}""";

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                string query;
                bool isRegex = false;

                try
                {
                    JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                    query = args.GetProperty("query").GetString() ?? "";
                    if (args.TryGetProperty("regex", out JsonElement regexProp))
                    {
                        isRegex = regexProp.GetBoolean();
                    }
                }
                catch
                {
                    query = input.Trim();
                }

                GitReflectionService service = GetService();
                IReadOnlyList<(string File, int Line, string Content)> results = await service.SearchCodeAsync(query, isRegex, ct);

                if (results.Count == 0)
                {
                    return Result<string, string>.Success($"No matches found for: {query}");
                }

                StringBuilder sb = new();
                sb.AppendLine($"\ud83d\udd0d **Found {results.Count} matches for:** `{query}`\n");

                foreach ((string file, int line, string content) in results.Take(20))
                {
                    sb.AppendLine($"**{file}:{line}**");
                    sb.AppendLine($"  `{content.Truncate(100)}`");
                }

                if (results.Count > 20)
                {
                    sb.AppendLine($"\n... and {results.Count - 20} more matches");
                }

                return Result<string, string>.Success(sb.ToString());
            }
            catch (IOException ex)
            {
                return Result<string, string>.Failure($"Search failed: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure($"Search failed: {ex.Message}");
            }
        }
    }
}
