// <copyright file="SearchMyThoughtsTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;
using Ouroboros.Application.Services;

/// <summary>
/// Search through my past thoughts semantically.
/// </summary>
internal class SearchMyThoughtsTool : ITool
{
    public string Name => "search_my_thoughts";
    public string Description => "Search through my past thoughts and memories using semantic similarity. Input: what to search for (e.g., 'consciousness', 'curiosity about AI').";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (SystemAccessTools.SharedPersistence == null)
        {
            return Result<string, string>.Failure("Self-persistence not available.");
        }

        var query = input.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result<string, string>.Failure("Please provide a search query.");
        }

        try
        {
            var thoughts = await SystemAccessTools.SharedPersistence.SearchRelatedThoughtsAsync(query, 5, ct);
            var facts = await SystemAccessTools.SharedPersistence.SearchRelatedFactsAsync(query, 3, ct);

            var sb = new StringBuilder();
            sb.AppendLine($"**Searching my memories for: {query}**\n");

            if (thoughts.Count > 0)
            {
                sb.AppendLine("**Related thoughts:**");
                foreach (var thought in thoughts)
                {
                    sb.AppendLine($"  [{thought.Type}] {thought.Content.Substring(0, Math.Min(150, thought.Content.Length))}...");
                }
            }

            if (facts.Count > 0)
            {
                sb.AppendLine("\n**Related learned facts:**");
                foreach (var fact in facts)
                {
                    sb.AppendLine($"  {fact}");
                }
            }

            if (thoughts.Count == 0 && facts.Count == 0)
            {
                sb.AppendLine("_No related memories found. I may not have thought about this yet._");
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Memory search failed: {ex.Message}");
        }
    }
}
