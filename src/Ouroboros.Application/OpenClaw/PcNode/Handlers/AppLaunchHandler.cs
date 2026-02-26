// <copyright file="AppLaunchHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Tools;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Launches an application.
/// Delegates to <see cref="SystemAccessTools.ProcessStartTool"/>.
/// Validates the application name against the security policy's whitelist.
/// </summary>
public sealed class AppLaunchHandler : IPcNodeCapabilityHandler
{
    private readonly PcNodeSecurityPolicy _policy;

    public AppLaunchHandler(PcNodeSecurityPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public string CapabilityName => "app.launch";
    public string Description => "Launch an application from the allowed whitelist";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Medium;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "program": { "type": "string", "description": "Application name or path to launch" },
            "args":    { "type": "string", "description": "Command-line arguments (default: empty)" },
            "wait":    { "type": "boolean", "description": "Wait for the process to exit (default: false)" }
          },
          "required": ["program"]
        }
        """;

    public bool RequiresApproval => false;

    public async Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var program = parameters.TryGetProperty("program", out var p) ? p.GetString() : null;
        if (string.IsNullOrWhiteSpace(program))
            return PcNodeResult.Fail("Missing required parameter 'program'");

        var verdict = _policy.ValidateProcess(program, ProcessOperation.Launch);
        if (!verdict.IsAllowed)
            return PcNodeResult.Fail(verdict.Reason!);

        var tool = new SystemAccessTools.ProcessStartTool();
        var result = await tool.InvokeAsync(parameters.GetRawText(), ct);
        return result.IsSuccess
            ? PcNodeResult.Ok(result.Value)
            : PcNodeResult.Fail(result.Error);
    }
}
