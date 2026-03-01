// <copyright file="ProcessKillHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Tools;
using Ouroboros.Application.Tools.SystemTools;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Kills a process by PID or name.
/// Delegates to <see cref="SystemAccessTools.ProcessKillTool"/>.
/// Validates the process name against the security policy's protected process list.
/// </summary>
public sealed class ProcessKillHandler : IPcNodeCapabilityHandler
{
    private readonly PcNodeSecurityPolicy _policy;

    public ProcessKillHandler(PcNodeSecurityPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public string CapabilityName => "process.kill";
    public string Description => "Kill a process by PID or name";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Medium;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "target": { "type": "string", "description": "Process ID (number) or process name" }
          },
          "required": ["target"]
        }
        """;

    public bool RequiresApproval => false;

    public async Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var target = parameters.TryGetProperty("target", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(target))
            return PcNodeResult.Fail("Missing required parameter 'target'");

        // Only validate against protected list for named processes (not PIDs)
        if (!int.TryParse(target, out _))
        {
            var verdict = _policy.ValidateProcess(target, ProcessOperation.Kill);
            if (!verdict.IsAllowed)
                return PcNodeResult.Fail(verdict.Reason!);
        }

        var tool = new ProcessKillTool();
        var result = await tool.InvokeAsync(target, ct);
        return result.IsSuccess
            ? PcNodeResult.Ok(result.Value)
            : PcNodeResult.Fail(result.Error);
    }
}
