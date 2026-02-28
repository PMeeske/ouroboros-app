// <copyright file="OpenClawNodeListTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// List connected device nodes with their capabilities (camera, SMS, location, etc.).
/// </summary>
public sealed class OpenClawNodeListTool : ITool
{
    public string Name => "openclaw_node_list";
    public string Description => "List connected device nodes with their capabilities (camera, SMS, location, etc.).";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();

        try
        {
            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("node.list", null, ct);
            return Result<string, string>.Success(result.ToString());
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to list nodes: {ex.Message}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
    }
}
