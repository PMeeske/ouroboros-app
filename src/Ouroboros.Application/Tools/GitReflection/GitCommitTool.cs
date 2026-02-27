// <copyright file="GitCommitTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Commits staged changes.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Commits staged changes.
    /// </summary>
    public class GitCommitTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "git_commit";

        /// <inheritdoc/>
        public string Description => "Commit staged changes. Input: commit message. Note: Message will be prefixed with '[Ouroboros Self-Modification]'.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                GitOperationResult result = await service.CommitAsync(input.Trim(), ct);

                return result.Success
                    ? Result<string, string>.Success($"\u2705 {result.Message}\nCommit: `{result.CommitHash}`")
                    : Result<string, string>.Failure(result.Message);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Commit failed: {ex.Message}");
            }
        }
    }
}
