// <copyright file="OpenClawDevicesApproveTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Approve a pending device pairing request.
/// </summary>
public sealed class OpenClawDevicesApproveTool : ITool
{
    public string Name => "openclaw_devices_approve";
    public string Description => "Approve a pending device pairing request.";
    public string? JsonSchema => """{"type":"object","properties":{"deviceId":{"type":"string","description":"Device ID to approve"}},"required":["deviceId"]}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();
        if (OpenClawSharedState.SharedPolicy == null)
            return OpenClawSharedState.PolicyNotInitialized();

        try
        {
            using var doc = JsonDocument.Parse(input);
            var deviceId = doc.RootElement.GetProperty("deviceId").GetString() ?? "";

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("devices.approve", new { deviceId }, ct);
            return Result<string, string>.Success($"Device '{deviceId}' approved. Response: {result}");
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"deviceId\":\"...\"}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to approve device: {ex.Message}");
        }
    }
}
