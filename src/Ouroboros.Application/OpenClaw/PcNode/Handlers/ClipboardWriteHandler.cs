// <copyright file="ClipboardWriteHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Tools;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Writes text to the system clipboard.
/// Delegates to <see cref="SystemAccessTools.ClipboardTool"/> (set action).
/// Enforces <see cref="PcNodeSecurityConfig.MaxClipboardLength"/>.
/// </summary>
public sealed class ClipboardWriteHandler : IPcNodeCapabilityHandler
{
    private readonly PcNodeSecurityConfig _config;

    public ClipboardWriteHandler(PcNodeSecurityConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public string CapabilityName => "clipboard.write";
    public string Description => "Write text to the system clipboard";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Low;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "text": { "type": "string", "description": "Text to write to the clipboard" }
          },
          "required": ["text"]
        }
        """;

    public bool RequiresApproval => false;

    public async Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var text = parameters.TryGetProperty("text", out var t) ? t.GetString() : null;
        if (text is null)
            return PcNodeResult.Fail("Missing required parameter 'text'");

        if (text.Length > _config.MaxClipboardLength)
            return PcNodeResult.Fail(
                $"Text length ({text.Length:N0}) exceeds maximum ({_config.MaxClipboardLength:N0} characters)");

        var tool = new SystemAccessTools.ClipboardTool();
        var input = JsonSerializer.Serialize(new { action = "set", text });
        var result = await tool.InvokeAsync(input, ct);
        return result.IsSuccess
            ? PcNodeResult.Ok(result.Value)
            : PcNodeResult.Fail(result.Error);
    }
}
