// <copyright file="OpenClawScreenRecordNodeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Record screen on a remote device node.
/// </summary>
public sealed class OpenClawScreenRecordNodeTool : ITool
{
    public string Name => "openclaw_screen_record_node";
    public string Description => "Record screen on a remote device node.";
    public string? JsonSchema => """{"type":"object","properties":{"node":{"type":"string","description":"Node identifier"},"duration":{"type":"integer","description":"Duration in seconds (default: 10, max: 60)"}},"required":["node"]}""";

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
            var node = root.GetProperty("node").GetString() ?? "";
            var duration = root.TryGetProperty("duration", out var d) ? d.GetInt32() : 10;

            var verdict = OpenClawSharedState.SharedPolicy.ValidateNodeInvoke(node, "screen.record", null);
            if (!verdict.IsAllowed)
                return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("node.invoke", new
            {
                node,
                command = "screen.record",
                @params = new { duration },
            }, ct);
            return Result<string, string>.Success($"Screen recording result: {result}");
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"node\":\"...\"}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to record screen: {ex.Message}");
        }
    }
}
