// <copyright file="GitBranchTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Creates a new Git branch.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Creates a new Git branch.
    /// </summary>
    public class GitBranchTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "git_create_branch";

        /// <inheritdoc/>
        public string Description => "Create a new Git branch for self-modification. Input: branch name (will be prefixed with 'ouroboros/self-modify/').";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                GitOperationResult result = await service.CreateBranchAsync(input.Trim(), ct: ct);

                return result.Success
                    ? Result<string, string>.Success($"\u2705 {result.Message}")
                    : Result<string, string>.Failure(result.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure($"Branch creation failed: {ex.Message}");
            }
        }
    }
}
