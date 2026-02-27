// <copyright file="OpenClawMemorySearchTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Semantic search across OpenClaw's persistent knowledge files (MEMORY.md, memory/*.md).
/// </summary>
public sealed class OpenClawMemorySearchTool : ITool
{
    public string Name => "openclaw_memory_search";
    public string Description => "Semantic search across OpenClaw's persistent knowledge files (MEMORY.md, memory/*.md).";
    public string? JsonSchema => """{"type":"object","properties":{"query":{"type":"string","description":"Search query"},"limit":{"type":"integer","description":"Maximum results (default: 10)"}},"required":["query"]}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;
            var query = root.GetProperty("query").GetString() ?? "";
            var limit = root.TryGetProperty("limit", out var l) ? l.GetInt32() : 10;

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("memory.search", new { query, limit }, ct);
            return Result<string, string>.Success(result.ToString());
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"query\":\"...\"}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to search memory: {ex.Message}");
        }
    }
}
