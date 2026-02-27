// <copyright file="OpenClawStatusTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Get OpenClaw Gateway health and connection info including circuit breaker states.
/// </summary>
public sealed class OpenClawStatusTool : ITool
{
    public string Name => "openclaw_status";
    public string Description => "Get OpenClaw Gateway health and connection info including circuit breaker states.";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();

        try
        {
            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("status", null, ct);
            var status = result.ToString();
            var resilience = OpenClawSharedState.SharedClient.Resilience.GetStatusSummary();
            return Result<string, string>.Success($"Gateway status: {status}\nResilience: {resilience}");
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to get gateway status: {ex.Message}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to get gateway status: {ex.Message}");
        }
    }
}
