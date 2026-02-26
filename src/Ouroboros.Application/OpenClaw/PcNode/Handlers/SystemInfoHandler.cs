// <copyright file="SystemInfoHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Tools;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Returns system information (OS, CPU, memory, uptime).
/// Delegates to <see cref="SystemAccessTools.SystemInfoTool"/>.
/// </summary>
public sealed class SystemInfoHandler : IPcNodeCapabilityHandler
{
    public string CapabilityName => "system.info";
    public string Description => "Get system information (OS, CPU, memory, uptime)";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Low;
    public string? ParameterSchema => null;
    public bool RequiresApproval => false;

    public async Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var tool = new SystemAccessTools.SystemInfoTool();
        var result = await tool.InvokeAsync(string.Empty, ct);
        return result.IsSuccess
            ? PcNodeResult.Ok(result.Value)
            : PcNodeResult.Fail(result.Error);
    }
}
