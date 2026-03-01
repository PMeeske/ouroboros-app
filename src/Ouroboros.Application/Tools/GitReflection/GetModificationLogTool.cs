// <copyright file="GetModificationLogTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Gets the modification log.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Gets the modification log.
    /// </summary>
    public class GetModificationLogTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "get_modification_log";

        /// <inheritdoc/>
        public string Description => "Get a summary of all self-modification proposals and their status.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            GitReflectionService service = GetService();
            string log = service.GetModificationSummary();
            return Result<string, string>.Success(log);
        }
    }
}
