// <copyright file="ProcessListHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Tools;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Lists running processes.
/// Delegates to <see cref="SystemAccessTools.ProcessListTool"/>.
/// </summary>
public sealed class ProcessListHandler : IPcNodeCapabilityHandler
{
    public string CapabilityName => "process.list";
    public string Description => "List running processes with optional name filter";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Medium;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "filter": { "type": "string", "description": "Optional process name filter" }
          }
        }
        """;

    public bool RequiresApproval => false;

    public async Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var filter = parameters.TryGetProperty("filter", out var f)
            ? f.GetString() ?? string.Empty
            : string.Empty;

        var tool = new SystemAccessTools.ProcessListTool();
        var result = await tool.InvokeAsync(filter, ct);
        return result.IsSuccess
            ? PcNodeResult.Ok(result.Value)
            : PcNodeResult.Fail(result.Error);
    }
}
