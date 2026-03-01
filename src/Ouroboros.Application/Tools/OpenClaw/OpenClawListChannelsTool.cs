// <copyright file="OpenClawListChannelsTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// List active messaging channels and their status (WhatsApp, Telegram, Slack, Discord, etc.).
/// </summary>
public sealed class OpenClawListChannelsTool : ITool
{
    public string Name => "openclaw_list_channels";
    public string Description => "List active messaging channels and their status (WhatsApp, Telegram, Slack, Discord, etc.).";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();

        try
        {
            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("channels", null, ct);
            return Result<string, string>.Success(result.ToString());
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to list channels: {ex.Message}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
    }
}
