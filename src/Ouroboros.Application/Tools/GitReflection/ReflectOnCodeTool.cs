// <copyright file="ReflectOnCodeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Reflects on own code for improvement opportunities.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Reflects on own code for improvement opportunities.
    /// </summary>
    public class ReflectOnCodeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "reflect_on_my_code";

        /// <inheritdoc/>
        public string Description => "Analyze a file in my codebase and identify potential improvements. Input: file path. Returns analysis with TODOs, issues, and improvement suggestions.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                string filePath = input.Trim();

                // Get file analysis
                CodeAnalysis analysis = await service.AnalyzeFileAsync(filePath, ct);

                StringBuilder sb = new();
                sb.AppendLine($"\ud83d\udd0d **Self-Reflection: {analysis.FilePath}**\n");

                // Summary
                sb.AppendLine("## Summary");
                sb.AppendLine($"- **Classes:** {analysis.Classes.Count}");
                sb.AppendLine($"- **Methods:** {analysis.Methods.Count}");
                sb.AppendLine($"- **Lines:** {analysis.TotalLines} ({analysis.CodeLines} code, {analysis.CommentLines} comments)");
                sb.AppendLine($"- **Comment Ratio:** {analysis.CommentRatio:P0}");

                // Issues
                if (analysis.PotentialIssues.Count > 0)
                {
                    sb.AppendLine("\n## \u26A0\uFE0F Issues Found");
                    foreach (string issue in analysis.PotentialIssues)
                    {
                        sb.AppendLine($"- {issue}");
                    }
                }

                // TODOs
                if (analysis.Todos.Count > 0)
                {
                    sb.AppendLine("\n## \ud83d\udcdd TODOs");
                    foreach (string todo in analysis.Todos)
                    {
                        sb.AppendLine($"- {todo}");
                    }
                }

                // Improvement suggestions
                sb.AppendLine("\n## \ud83d\udca1 Improvement Opportunities");

                if (analysis.CommentRatio < 0.1)
                {
                    sb.AppendLine("- **Documentation:** Comment ratio is low. Consider adding XML documentation.");
                }

                if (analysis.Methods.Count > 20)
                {
                    sb.AppendLine("- **Refactoring:** Large number of methods. Consider extracting related methods to separate classes.");
                }

                if (analysis.TotalLines > 500)
                {
                    sb.AppendLine("- **Size:** File is large. Consider splitting into multiple files by responsibility.");
                }

                if (analysis.PotentialIssues.Any(i => i.Contains("NotImplementedException")))
                {
                    sb.AppendLine("- **Completeness:** Contains unimplemented methods. Complete the implementation.");
                }

                sb.AppendLine($"\nUse `self_modify` to apply improvements to this file.");

                return Result<string, string>.Success(sb.ToString());
            }
            catch (IOException ex)
            {
                return Result<string, string>.Failure($"Reflection failed: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure($"Reflection failed: {ex.Message}");
            }
        }
    }
}
