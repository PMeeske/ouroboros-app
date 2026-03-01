// <copyright file="ClipboardReadHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Tools;
using Ouroboros.Application.Tools.SystemTools;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Reads the system clipboard contents.
/// Delegates to <see cref="SystemAccessTools.ClipboardTool"/> (get action).
/// Outbound sensitive data scanning is performed by <see cref="OpenClawPcNode"/> after execution.
/// </summary>
public sealed class ClipboardReadHandler : IPcNodeCapabilityHandler
{
    public string CapabilityName => "clipboard.read";
    public string Description => "Read the current clipboard contents";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Low;
    public string? ParameterSchema => null;
    public bool RequiresApproval => false;

    public async Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var tool = new ClipboardTool();
        var input = JsonSerializer.Serialize(new { action = "get" });
        var result = await tool.InvokeAsync(input, ct);
        return result.IsSuccess
            ? PcNodeResult.Ok(result.Value)
            : PcNodeResult.Fail(result.Error);
    }
}
