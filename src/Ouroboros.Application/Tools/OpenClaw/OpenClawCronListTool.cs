// <copyright file="OpenClawCronListTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// List all scheduled jobs in OpenClaw.
/// </summary>
public sealed class OpenClawCronListTool : ITool
{
    public string Name => "openclaw_cron_list";
    public string Description => "List all scheduled jobs in OpenClaw.";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();

        try
        {
            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("cron.list", null, ct);
            return Result<string, string>.Success(result.ToString());
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to list cron jobs: {ex.Message}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to list cron jobs: {ex.Message}");
        }
    }
}
