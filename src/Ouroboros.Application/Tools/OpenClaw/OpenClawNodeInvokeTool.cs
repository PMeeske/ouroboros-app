// <copyright file="OpenClawNodeInvokeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Execute an action on a connected device node (camera.snap, sms.send, location.get, screen.record, etc.).
/// </summary>
public sealed class OpenClawNodeInvokeTool : ITool
{
    public string Name => "openclaw_node_invoke";
    public string Description => "Execute an action on a connected device node (camera.snap, sms.send, location.get, screen.record, etc.).";
    public string? JsonSchema => """{"type":"object","properties":{"node":{"type":"string","description":"Node identifier (e.g. 'phone', 'macbook')"},"command":{"type":"string","description":"Command to execute (camera.snap, sms.send, location.get, etc.)"},"params":{"type":"object","description":"Optional command parameters"}},"required":["node","command"]}""";

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
            var command = root.GetProperty("command").GetString() ?? "";
            var paramsJson = root.TryGetProperty("params", out var p) ? p.ToString() : null;

            // Security policy check (mandatory)
            var verdict = OpenClawSharedState.SharedPolicy.ValidateNodeInvoke(node, command, paramsJson);
            if (!verdict.IsAllowed)
                return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("node.invoke", new
            {
                node,
                command,
                @params = paramsJson != null ? JsonSerializer.Deserialize<JsonElement>(paramsJson) : (object?)null,
            }, ct);

            return Result<string, string>.Success($"Node invoke result ({node}/{command}): {result}");
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON input. Expected: {\"node\":\"...\",\"command\":\"...\",\"params\":{}}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to invoke node command: {ex.Message}");
        }
    }
}
