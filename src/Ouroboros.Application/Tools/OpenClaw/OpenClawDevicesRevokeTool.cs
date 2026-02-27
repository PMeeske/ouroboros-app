// <copyright file="OpenClawDevicesRevokeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Revoke a paired device's access to the gateway.
/// </summary>
public sealed class OpenClawDevicesRevokeTool : ITool
{
    public string Name => "openclaw_devices_revoke";
    public string Description => "Revoke a paired device's access to the gateway.";
    public string? JsonSchema => """{"type":"object","properties":{"deviceId":{"type":"string","description":"Device ID to revoke"}},"required":["deviceId"]}""";

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

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("devices.revoke", new { deviceId }, ct);
            return Result<string, string>.Success($"Device '{deviceId}' revoked. Response: {result}");
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
            return Result<string, string>.Failure($"Failed to revoke device: {ex.Message}");
        }
    }
}
