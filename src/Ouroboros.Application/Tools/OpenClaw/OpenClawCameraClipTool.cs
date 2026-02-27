// <copyright file="OpenClawCameraClipTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Record a short video clip on a paired phone/camera node.
/// </summary>
public sealed class OpenClawCameraClipTool : ITool
{
    public string Name => "openclaw_camera_clip";
    public string Description => "Record a short video clip on a paired phone/camera node.";
    public string? JsonSchema => """{"type":"object","properties":{"node":{"type":"string","description":"Node identifier"},"duration":{"type":"integer","description":"Duration in seconds (default: 5, max: 30)"},"camera":{"type":"string","description":"Camera: 'front' or 'back' (default: 'back')"}},"required":["node"]}""";

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
            var duration = root.TryGetProperty("duration", out var d) ? d.GetInt32() : 5;
            var camera = root.TryGetProperty("camera", out var c) ? c.GetString() ?? "back" : "back";

            var verdict = OpenClawSharedState.SharedPolicy.ValidateNodeInvoke(node, "camera.clip", null);
            if (!verdict.IsAllowed)
                return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("node.invoke", new
            {
                node,
                command = "camera.clip",
                @params = new { duration, camera },
            }, ct);
            return Result<string, string>.Success($"Camera clip result: {result}");
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"node\":\"...\"}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to record camera clip: {ex.Message}");
        }
    }
}
