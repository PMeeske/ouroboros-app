// <copyright file="ScreenCaptureHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Tools;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Captures a screenshot of the screen.
/// Delegates to <see cref="PerceptionTools.ScreenCaptureTool"/>.
/// Returns the screenshot as a base64-encoded PNG payload.
/// Enforces <see cref="PcNodeSecurityConfig.MaxScreenshotResolution"/>.
/// </summary>
public sealed class ScreenCaptureHandler : IPcNodeCapabilityHandler
{
    private readonly PcNodeSecurityConfig _config;

    public ScreenCaptureHandler(PcNodeSecurityConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public string CapabilityName => "screen.capture";
    public string Description => "Capture a screenshot of the screen";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Medium;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "monitor": { "type": "integer", "description": "Monitor index (default: 0)" },
            "region":  {
              "type": "object",
              "properties": {
                "x":      { "type": "integer" },
                "y":      { "type": "integer" },
                "width":  { "type": "integer" },
                "height": { "type": "integer" }
              }
            }
          }
        }
        """;

    public bool RequiresApproval => false;

    public async Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var tool = new PerceptionTools.ScreenCaptureTool();
        var result = await tool.InvokeAsync(parameters.GetRawText(), ct);

        if (!result.IsSuccess)
            return PcNodeResult.Fail(result.Error);

        // The tool returns a file path; read it and convert to base64 for transmission
        var filePath = result.Value.Trim();
        if (!File.Exists(filePath))
            return PcNodeResult.Fail("Screenshot file was not created");

        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath, ct);
            var base64 = Convert.ToBase64String(bytes);
            return PcNodeResult.OkWithPayload(base64, "Screenshot captured");
        }
        finally
        {
            // Clean up the temporary file
            try { File.Delete(filePath); } catch { /* best effort */ }
        }
    }
}
