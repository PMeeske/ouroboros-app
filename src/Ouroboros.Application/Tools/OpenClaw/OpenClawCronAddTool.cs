// <copyright file="OpenClawCronAddTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Create a scheduled job in OpenClaw. Supports every/at/cron schedule types.
/// </summary>
public sealed class OpenClawCronAddTool : ITool
{
    public string Name => "openclaw_cron_add";
    public string Description => "Create a scheduled job in OpenClaw. Supports every/at/cron schedule types.";
    public string? JsonSchema => """
        {
          "type": "object",
          "properties": {
            "name":          { "type": "string",  "description": "Job name (unique identifier)" },
            "sessionTarget": { "type": "string",  "description": "Target session key (e.g. 'agent:main:main')" },
            "scheduleKind":  { "type": "string",  "enum": ["every","at","cron"], "description": "Schedule type" },
            "everyMs":       { "type": "integer", "description": "Interval in ms (for scheduleKind='every', e.g. 3600000 = 1h)" },
            "atMs":          { "type": "integer", "description": "Unix timestamp in ms (for scheduleKind='at')" },
            "cronExpr":      { "type": "string",  "description": "Cron expression (for scheduleKind='cron', e.g. '0 9 * * *')" },
            "action":        { "type": "string",  "description": "Message/prompt to run on schedule" }
          },
          "required": ["name", "sessionTarget", "scheduleKind", "action"]
        }
        """;

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
            var name          = root.GetProperty("name").GetString() ?? "";
            var sessionTarget = root.GetProperty("sessionTarget").GetString() ?? "";
            var scheduleKind  = root.GetProperty("scheduleKind").GetString() ?? "every";
            var action        = root.GetProperty("action").GetString() ?? "";

            object schedule = scheduleKind switch
            {
                "at"   => (object)new { kind = "at",   at   = root.GetProperty("atMs").GetInt64() },
                "cron" => (object)new { kind = "cron", expr = root.GetProperty("cronExpr").GetString() ?? "" },
                _      => (object)new { kind = "every", everyMs = root.TryGetProperty("everyMs", out var em) ? em.GetInt64() : 3_600_000L },
            };

            var payload = new { message = action };

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync(
                "cron.add", new { name, sessionTarget, schedule, payload }, ct);
            return Result<string, string>.Success($"Job '{name}' scheduled on '{sessionTarget}'. Response: {result}");
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"name\":\"...\",\"sessionTarget\":\"agent:main:main\",\"scheduleKind\":\"every\",\"everyMs\":3600000,\"action\":\"...\"}");
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
