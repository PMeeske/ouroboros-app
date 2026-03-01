// <copyright file="ScreenRecordHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Tools;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Records screen changes over a time period.
/// Delegates to <see cref="PerceptionTools.WatchScreenTool"/>.
/// Enforces <see cref="PcNodeSecurityConfig.MaxScreenRecordSeconds"/>.
/// </summary>
public sealed class ScreenRecordHandler : IPcNodeCapabilityHandler
{
    private readonly PcNodeSecurityConfig _config;

    public ScreenRecordHandler(PcNodeSecurityConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public string CapabilityName => "screen.record";
    public string Description => "Watch the screen for changes over a time period";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.High;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "duration_seconds": { "type": "integer", "description": "Recording duration in seconds (default: 30, max configurable)" },
            "interval_ms":     { "type": "integer", "description": "Capture interval in milliseconds (default: 1000)" },
            "sensitivity":     { "type": "number",  "description": "Change detection sensitivity 0.0-1.0 (default: 0.1)" }
          }
        }
        """;

    public bool RequiresApproval => true;

    public async Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        // Enforce maximum recording duration
        var requestedDuration = parameters.TryGetProperty("duration_seconds", out var d)
            ? d.GetInt32()
            : 30;

        if (requestedDuration > _config.MaxScreenRecordSeconds)
        {
            return PcNodeResult.Fail(
                $"Requested duration ({requestedDuration}s) exceeds maximum ({_config.MaxScreenRecordSeconds}s)");
        }

        var tool = new PerceptionTools.WatchScreenTool();
        var result = await tool.InvokeAsync(parameters.GetRawText(), ct);
        return result.IsSuccess
            ? PcNodeResult.Ok(result.Value)
            : PcNodeResult.Fail(result.Error);
    }
}
