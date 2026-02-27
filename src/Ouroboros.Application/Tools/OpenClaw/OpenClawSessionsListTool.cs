// <copyright file="OpenClawSessionsListTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// List active OpenClaw sessions with agents, channels, and timestamps.
/// </summary>
public sealed class OpenClawSessionsListTool : ITool
{
    public string Name => "openclaw_sessions_list";
    public string Description => "List active OpenClaw sessions with agents, channels, and timestamps.";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();

        try
        {
            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("sessions.list", null, ct);
            return Result<string, string>.Success(result.ToString());
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to list sessions: {ex.Message}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to list sessions: {ex.Message}");
        }
    }
}
