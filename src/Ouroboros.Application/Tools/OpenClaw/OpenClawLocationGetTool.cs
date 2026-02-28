// <copyright file="OpenClawLocationGetTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Get GPS location from a paired phone node.
/// </summary>
public sealed class OpenClawLocationGetTool : ITool
{
    public string Name => "openclaw_location_get";
    public string Description => "Get GPS location from a paired phone node.";
    public string? JsonSchema => """{"type":"object","properties":{"node":{"type":"string","description":"Node identifier (e.g. 'phone')"}},"required":["node"]}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();
        if (OpenClawSharedState.SharedPolicy == null)
            return OpenClawSharedState.PolicyNotInitialized();

        try
        {
            using var doc = JsonDocument.Parse(input);
            var node = doc.RootElement.GetProperty("node").GetString() ?? "";

            var verdict = OpenClawSharedState.SharedPolicy.ValidateNodeInvoke(node, "location.get", null);
            if (!verdict.IsAllowed)
                return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("node.invoke", new
            {
                node,
                command = "location.get",
            }, ct);
            return Result<string, string>.Success($"Location: {result}");
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
            return Result<string, string>.Failure($"Failed to get location: {ex.Message}");
        }
    }
}
