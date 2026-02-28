// <copyright file="ListSourceFilesTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Lists source files in the repository.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Lists source files in the repository.
    /// </summary>
    public class ListSourceFilesTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "list_my_files";

        /// <inheritdoc/>
        public string Description => "List my source files. Optional input: filter pattern (e.g., 'Tools' to list only files with 'Tools' in the path).";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                string? filter = string.IsNullOrWhiteSpace(input) ? null : input.Trim();
                IReadOnlyList<RepoFileInfo> files = await service.ListSourceFilesAsync(filter, ct);

                StringBuilder sb = new();
                sb.AppendLine($"\ud83d\udcc1 **Source Files{(filter != null ? $" matching '{filter}'" : "")}** ({files.Count} files)\n");

                foreach (RepoFileInfo file in files.Take(50))
                {
                    string sizeStr = file.SizeBytes > 10000 ? $"{file.SizeBytes / 1024}KB" : $"{file.SizeBytes}B";
                    sb.AppendLine($"  \ud83d\udcc4 {file.RelativePath} ({file.LineCount} lines, {sizeStr})");
                }

                if (files.Count > 50)
                {
                    sb.AppendLine($"\n... and {files.Count - 50} more files");
                }

                return Result<string, string>.Success(sb.ToString());
            }
            catch (IOException ex)
            {
                return Result<string, string>.Failure($"Failed to list files: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure($"Failed to list files: {ex.Message}");
            }
        }
    }
}
