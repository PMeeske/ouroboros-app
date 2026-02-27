// <copyright file="FileListHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Tools;
using Ouroboros.Application.Tools.SystemTools;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Lists files and folders in a directory.
/// Delegates to <see cref="SystemAccessTools.DirectoryListTool"/>.
/// Validates the path against the security policy's path jail.
/// </summary>
public sealed class FileListHandler : IPcNodeCapabilityHandler
{
    private readonly PcNodeSecurityPolicy _policy;

    public FileListHandler(PcNodeSecurityPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public string CapabilityName => "file.list";
    public string Description => "List files and folders in a directory";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Medium;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "path": { "type": "string", "description": "Directory path to list" }
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

        var verdict = _policy.ValidateFilePath(path, FileOperation.List);
        if (!verdict.IsAllowed)
            return PcNodeResult.Fail(verdict.Reason!);

        var tool = new DirectoryListTool();
        var result = await tool.InvokeAsync(path, ct);
        return result.IsSuccess
            ? PcNodeResult.Ok(result.Value)
            : PcNodeResult.Fail(result.Error);
    }
}
