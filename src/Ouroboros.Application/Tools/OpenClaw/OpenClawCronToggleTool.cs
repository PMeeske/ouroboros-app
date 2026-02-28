// <copyright file="OpenClawCronToggleTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Enable or disable a scheduled job.
/// </summary>
public sealed class OpenClawCronToggleTool : ITool
{
    public string Name => "openclaw_cron_toggle";
    public string Description => "Enable or disable a scheduled job.";
    public string? JsonSchema => """{"type":"object","properties":{"name":{"type":"string","description":"Job name"},"enabled":{"type":"boolean","description":"true to enable, false to disable"}},"required":["name","enabled"]}""";

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
            var enabled = root.GetProperty("enabled").GetBoolean();

            var method = enabled ? "cron.enable" : "cron.disable";
            var result = await OpenClawSharedState.SharedClient.SendRequestAsync(method, new { name }, ct);
            return Result<string, string>.Success($"Job '{name}' {(enabled ? "enabled" : "disabled")}. Response: {result}");
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"name\":\"...\", \"enabled\":true}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to toggle cron job: {ex.Message}");
        }
    }
}
