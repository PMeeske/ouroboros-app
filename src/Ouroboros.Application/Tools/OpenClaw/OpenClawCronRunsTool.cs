// <copyright file="OpenClawCronRunsTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// View execution history for a scheduled job.
/// </summary>
public sealed class OpenClawCronRunsTool : ITool
{
    public string Name => "openclaw_cron_runs";
    public string Description => "View execution history for a scheduled job.";
    public string? JsonSchema => """{"type":"object","properties":{"name":{"type":"string","description":"Job name"},"limit":{"type":"integer","description":"Maximum runs to return (default: 10)"}},"required":["name"]}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;
            var name = root.GetProperty("name").GetString() ?? "";
            var limit = root.TryGetProperty("limit", out var l) ? l.GetInt32() : 10;

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("cron.runs", new { name, limit }, ct);
            return Result<string, string>.Success(result.ToString());
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"name\":\"...\"}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to get cron runs: {ex.Message}");
        }
    }
}
