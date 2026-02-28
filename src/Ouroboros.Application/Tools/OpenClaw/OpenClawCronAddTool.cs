// <copyright file="OpenClawCronAddTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Create a scheduled job in OpenClaw. Supports at/every/cron syntax.
/// </summary>
public sealed class OpenClawCronAddTool : ITool
{
    public string Name => "openclaw_cron_add";
    public string Description => "Create a scheduled job in OpenClaw. Supports at/every/cron syntax.";
    public string? JsonSchema => """{"type":"object","properties":{"name":{"type":"string","description":"Job name"},"schedule":{"type":"string","description":"Schedule expression (e.g. 'every 1h', 'at 09:00', '0 */2 * * *')"},"action":{"type":"string","description":"Action to execute (prompt or command)"},"channel":{"type":"string","description":"Optional channel to run in"}},"required":["name","schedule","action"]}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();
        if (OpenClawSharedState.SharedPolicy == null)
            return OpenClawSharedState.PolicyNotInitialized();

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;
            var name = root.GetProperty("name").GetString() ?? "";
            var schedule = root.GetProperty("schedule").GetString() ?? "";
            var action = root.GetProperty("action").GetString() ?? "";
            var channel = root.TryGetProperty("channel", out var ch) ? ch.GetString() : null;

            var p = channel != null
                ? (object)new { name, schedule, action, channel }
                : new { name, schedule, action };

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("cron.add", p, ct);
            return Result<string, string>.Success($"Job '{name}' created. Response: {result}");
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"name\":\"...\", \"schedule\":\"...\", \"action\":\"...\"}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to add cron job: {ex.Message}");
        }
    }
}
