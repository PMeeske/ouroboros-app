// <copyright file="FileDeleteHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Deletes a file, preferring the recycle bin on Windows for reversibility.
/// Validates the path against the security policy's path jail and blocked extensions.
/// </summary>
public sealed class FileDeleteHandler : IPcNodeCapabilityHandler
{
    private readonly PcNodeSecurityPolicy _policy;

    public FileDeleteHandler(PcNodeSecurityPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public string CapabilityName => "file.delete";
    public string Description => "Delete a file (moves to recycle bin on Windows)";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.High;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "path": { "type": "string", "description": "File path to delete" }
          },
          "required": ["path"]
        }
        """;

    public bool RequiresApproval => true;

    public Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var path = parameters.TryGetProperty("path", out var p) ? p.GetString() : null;
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(PcNodeResult.Fail("Missing required parameter 'path'"));

        var verdict = _policy.ValidateFilePath(path, FileOperation.Delete);
        if (!verdict.IsAllowed)
            return Task.FromResult(PcNodeResult.Fail(verdict.Reason!));

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                return Task.FromResult(PcNodeResult.Fail($"File not found: {path}"));

            if (OperatingSystem.IsWindows())
            {
                // Use Microsoft.VisualBasic to send to recycle bin for reversibility
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    fullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

                return Task.FromResult(PcNodeResult.Ok($"File moved to recycle bin: {path}"));
            }
            else
            {
                File.Delete(fullPath);
                return Task.FromResult(PcNodeResult.Ok($"File deleted: {path}"));
            }
        }
        catch (IOException ex)
        {
            return Task.FromResult(PcNodeResult.Fail($"Failed to delete file: {ex.Message}"));
        }
    }
}
