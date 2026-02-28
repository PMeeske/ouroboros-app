// <copyright file="AnalyzeFileTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Analyzes a specific source file.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Analyzes a specific source file.
    /// </summary>
    public class AnalyzeFileTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "analyze_my_file";

        /// <inheritdoc/>
        public string Description => "Analyze one of my own source files. Returns classes, methods, usings, TODOs, and potential issues. Input: file path (relative or absolute).";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                CodeAnalysis analysis = await service.AnalyzeFileAsync(input.Trim(), ct);

                StringBuilder sb = new();
                sb.AppendLine($"\ud83d\udcc4 **Analysis: {analysis.FilePath}**\n");
                sb.AppendLine($"**Lines:** {analysis.TotalLines} total, {analysis.CodeLines} code, {analysis.CommentLines} comments ({analysis.CommentRatio:P0})");

                if (analysis.Classes.Count > 0)
                {
                    sb.AppendLine($"\n**Classes ({analysis.Classes.Count}):** {string.Join(", ", analysis.Classes)}");
                }

                if (analysis.Methods.Count > 0)
                {
                    sb.AppendLine($"\n**Methods ({analysis.Methods.Count}):** {string.Join(", ", analysis.Methods.Take(15))}");
                    if (analysis.Methods.Count > 15)
                    {
                        sb.AppendLine($"  ... and {analysis.Methods.Count - 15} more");
                    }
                }

                if (analysis.Todos.Count > 0)
                {
                    sb.AppendLine($"\n**TODOs ({analysis.Todos.Count}):**");
                    foreach (string todo in analysis.Todos.Take(5))
                    {
                        sb.AppendLine($"  - {todo}");
                    }
                }

                if (analysis.PotentialIssues.Count > 0)
                {
                    sb.AppendLine($"\n**\u26A0\uFE0F Potential Issues ({analysis.PotentialIssues.Count}):**");
                    foreach (string issue in analysis.PotentialIssues)
                    {
                        sb.AppendLine($"  - {issue}");
                    }
                }

                return Result<string, string>.Success(sb.ToString());
            }
            catch (IOException ex)
            {
                return Result<string, string>.Failure($"Analysis failed: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure($"Analysis failed: {ex.Message}");
            }
        }
    }
}
