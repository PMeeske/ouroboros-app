// <copyright file="FileReadHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Tools;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Reads file contents.
/// Delegates to <see cref="SystemAccessTools.FileReadTool"/>.
/// Validates the path against the security policy's path jail and file size limit.
/// Outbound sensitive data scanning is performed by <see cref="OpenClawPcNode"/> after execution.
/// </summary>
public sealed class FileReadHandler : IPcNodeCapabilityHandler
{
    private readonly PcNodeSecurityPolicy _policy;

    public FileReadHandler(PcNodeSecurityPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public string CapabilityName => "file.read";
    public string Description => "Read the contents of a file";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Medium;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "path":  { "type": "string", "description": "File path to read" },
            "lines": { "type": "integer", "description": "Max lines to read (default: all)" }
          },
          "required": ["path"]
        }
        """;

    public bool RequiresApproval => false;

    public async Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var path = parameters.TryGetProperty("path", out var p) ? p.GetString() : null;
        if (string.IsNullOrWhiteSpace(path))
            return PcNodeResult.Fail("Missing required parameter 'path'");

        var verdict = _policy.ValidateFilePath(path, FileOperation.Read);
        if (!verdict.IsAllowed)
            return PcNodeResult.Fail(verdict.Reason!);

        var tool = new SystemAccessTools.FileReadTool();
        var result = await tool.InvokeAsync(parameters.GetRawText(), ct);
        return result.IsSuccess
            ? PcNodeResult.Ok(result.Value)
            : PcNodeResult.Fail(result.Error);
    }
}
