// <copyright file="OpenClawMemoryGetTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Retrieve a specific memory entry by key from OpenClaw's knowledge store.
/// </summary>
public sealed class OpenClawMemoryGetTool : ITool
{
    public string Name => "openclaw_memory_get";
    public string Description => "Retrieve a specific memory entry by key from OpenClaw's knowledge store.";
    public string? JsonSchema => """{"type":"object","properties":{"key":{"type":"string","description":"Memory entry key"}},"required":["key"]}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();

        try
        {
            using var doc = JsonDocument.Parse(input);
            var key = doc.RootElement.GetProperty("key").GetString() ?? "";

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("memory.get", new { key }, ct);
            return Result<string, string>.Success(result.ToString());
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"key\":\"...\"}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to get memory entry: {ex.Message}");
        }
    }
}
