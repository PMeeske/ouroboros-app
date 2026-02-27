// <copyright file="OpenClawHealthTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Get detailed OpenClaw gateway health including provider status, uptime, and connected nodes.
/// </summary>
public sealed class OpenClawHealthTool : ITool
{
    public string Name => "openclaw_health";
    public string Description => "Get detailed OpenClaw gateway health including provider status, uptime, and connected nodes.";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();

        try
        {
            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("health", null, ct);
            return Result<string, string>.Success(result.ToString());
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to get gateway health: {ex.Message}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to get gateway health: {ex.Message}");
        }
    }
}
