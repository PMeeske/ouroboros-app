// <copyright file="GitStatusTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Gets the current Git status.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Gets the current Git status.
    /// </summary>
    public class GitStatusTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "git_status";

        /// <inheritdoc/>
        public string Description => "Get the current Git status showing modified, staged, and untracked files.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                string status = await service.GetStatusAsync(ct);
                string branch = await service.GetCurrentBranchAsync(ct);

                return Result<string, string>.Success($"**Branch:** `{branch}`\n\n{status}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Git status failed: {ex.Message}");
            }
        }
    }
}
