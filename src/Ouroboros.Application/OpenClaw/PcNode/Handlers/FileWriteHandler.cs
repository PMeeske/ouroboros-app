// <copyright file="FileWriteHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Tools;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Writes content to a file.
/// Delegates to <see cref="SystemAccessTools.FileWriteTool"/>.
/// Validates the path against the security policy's path jail and blocked extensions.
/// </summary>
public sealed class FileWriteHandler : IPcNodeCapabilityHandler
{
    private readonly PcNodeSecurityPolicy _policy;

    public FileWriteHandler(PcNodeSecurityPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public string CapabilityName => "file.write";
    public string Description => "Write content to a file (creates or overwrites)";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Medium;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "path":    { "type": "string", "description": "File path to write" },
            "content": { "type": "string", "description": "Content to write" },
            "append":  { "type": "boolean", "description": "Append instead of overwrite (default: false)" }
          },
          "required": ["path", "content"]
        }
        """;

    public bool RequiresApproval => false;

    public async Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var path = parameters.TryGetProperty("path", out var p) ? p.GetString() : null;
        if (string.IsNullOrWhiteSpace(path))
            return PcNodeResult.Fail("Missing required parameter 'path'");

        var verdict = _policy.ValidateFilePath(path, FileOperation.Write);
        if (!verdict.IsAllowed)
            return PcNodeResult.Fail(verdict.Reason!);

        var tool = new SystemAccessTools.FileWriteTool();
        var result = await tool.InvokeAsync(parameters.GetRawText(), ct);
        return result.IsSuccess
            ? PcNodeResult.Ok(result.Value)
            : PcNodeResult.Fail(result.Error);
    }
}
