// <copyright file="OpenClawCameraSnapTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Take a photo on a paired phone/camera node. Returns the image data.
/// </summary>
public sealed class OpenClawCameraSnapTool : ITool
{
    public string Name => "openclaw_camera_snap";
    public string Description => "Take a photo on a paired phone/camera node. Returns the image data.";
    public string? JsonSchema => """{"type":"object","properties":{"node":{"type":"string","description":"Node identifier (e.g. 'phone')"},"camera":{"type":"string","description":"Camera to use: 'front' or 'back' (default: 'back')"}},"required":["node"]}""";

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
            var camera = root.TryGetProperty("camera", out var c) ? c.GetString() ?? "back" : "back";

            var verdict = OpenClawSharedState.SharedPolicy.ValidateNodeInvoke(node, "camera.snap", null);
            if (!verdict.IsAllowed)
                return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("node.invoke", new
            {
                node,
                command = "camera.snap",
                @params = new { camera },
            }, ct);
            return Result<string, string>.Success($"Camera snap result: {result}");
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
            return Result<string, string>.Failure($"Failed to snap camera: {ex.Message}");
        }
    }
}
