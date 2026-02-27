// <copyright file="GetCodebaseOverviewTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Gets an overview of the entire codebase.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Gets an overview of the entire codebase.
    /// </summary>
    public class GetCodebaseOverviewTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "get_codebase_overview";

        /// <inheritdoc/>
        public string Description => "Get a high-level overview of my own codebase including directory structure, file counts, and line counts. Use this to understand the overall architecture.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                string overview = await service.GetCodebaseOverviewAsync(ct);
                return Result<string, string>.Success(overview);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get codebase overview: {ex.Message}");
            }
        }
    }
}
