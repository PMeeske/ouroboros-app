// <copyright file="GitReflectionCliSteps.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using Ouroboros.Application.Tools;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application;

/// <summary>
/// CLI Pipeline steps for Git-based code reflection and self-modification.
/// Enables Ouroboros to analyze, understand, and modify its own source code through the pipeline DSL.
/// </summary>
public static partial class GitReflectionCliSteps
{
    private static GitReflectionService? _service;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets or initializes the Git reflection service.
    /// </summary>
    private static GitReflectionService GetService()
    {
        if (_service == null)
        {
            lock (_lock)
            {
                _service ??= new GitReflectionService();
            }
        }
        return _service;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CODE REFLECTION STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get an overview of the Ouroboros codebase.
    /// Usage: CodebaseOverview()
    /// </summary>
    [PipelineToken("CodebaseOverview", "MyCode")]
    public static Step<CliPipelineState, CliPipelineState> CodebaseOverview(string? args = null)
        => async s =>
        {
            try
            {
                GitReflectionService service = GetService();
                string overview = await service.GetCodebaseOverviewAsync();

                Console.WriteLine(overview);
                s.Output = overview;
                s.Context = "Codebase overview generated";
            }
            catch (OperationCanceledException) { throw; }
            catch (IOException ex)
            {
                Console.WriteLine($"[git] Failed to get codebase overview: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[git] Failed to get codebase overview: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Analyze a specific source file for reflection.
    /// Usage: AnalyzeFile('src/Ouroboros.Application/CliSteps.cs')
    /// </summary>
    [PipelineToken("AnalyzeFile", "ReflectFile")]
    public static Step<CliPipelineState, CliPipelineState> AnalyzeFile(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: AnalyzeFile('path/to/file.cs')");
                return s;
            }

            try
            {
                GitReflectionService service = GetService();
                CodeAnalysis analysis = await service.AnalyzeFileAsync(args.Trim());

                StringBuilder sb = new();
                sb.AppendLine($"📄 Analysis: {analysis.FilePath}");
                sb.AppendLine($"   Lines: {analysis.TotalLines} total, {analysis.CodeLines} code, {analysis.CommentLines} comments");
                sb.AppendLine($"   Classes: {string.Join(", ", analysis.Classes)}");
                sb.AppendLine($"   Methods: {analysis.Methods.Count}");

                if (analysis.Todos.Count > 0)
                {
                    sb.AppendLine($"   TODOs: {analysis.Todos.Count}");
                    foreach (string todo in analysis.Todos.Take(3))
                    {
                        sb.AppendLine($"      - {todo}");
                    }
                }

                if (analysis.PotentialIssues.Count > 0)
                {
                    sb.AppendLine($"   ⚠️ Issues: {analysis.PotentialIssues.Count}");
                    foreach (string issue in analysis.PotentialIssues)
                    {
                        sb.AppendLine($"      - {issue}");
                    }
                }

                Console.WriteLine(sb.ToString());
                s.Output = sb.ToString();
                s.Context = $"Analyzed {analysis.FilePath}";
            }
            catch (OperationCanceledException) { throw; }
            catch (IOException ex)
            {
                Console.WriteLine($"[git] Analysis failed: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[git] Analysis failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Search the codebase for a pattern.
    /// Usage: SearchCode('pattern')
    /// Usage: SearchCode('pattern;regex') - for regex search
    /// </summary>
    [PipelineToken("SearchCode", "FindCode")]
    public static Step<CliPipelineState, CliPipelineState> SearchCode(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: SearchCode('pattern') or SearchCode('pattern;regex')");
                return s;
            }

            try
            {
                string[] parts = args.Split(';');
                string pattern = parts[0].Trim();
                bool isRegex = parts.Length > 1 && parts[1].Trim().ToLowerInvariant() == "regex";

                GitReflectionService service = GetService();
                IReadOnlyList<(string File, int Line, string Content)> results = await service.SearchCodeAsync(pattern, isRegex);

                StringBuilder sb = new();
                sb.AppendLine($"🔍 Found {results.Count} matches for: {pattern}");

                foreach ((string file, int line, string content) in results.Take(15))
                {
                    sb.AppendLine($"   {file}:{line} - {content.Truncate(60)}");
                }

                if (results.Count > 15)
                {
                    sb.AppendLine($"   ... and {results.Count - 15} more");
                }

                Console.WriteLine(sb.ToString());
                s.Output = sb.ToString();
                s.Context = $"Found {results.Count} matches for '{pattern}'";
            }
            catch (OperationCanceledException) { throw; }
            catch (IOException ex)
            {
                Console.WriteLine($"[git] Search failed: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[git] Search failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// List source files matching a filter.
    /// Usage: ListFiles() - list all files
    /// Usage: ListFiles('Application') - filter by path
    /// </summary>
    [PipelineToken("ListFiles", "MyFiles")]
    public static Step<CliPipelineState, CliPipelineState> ListFiles(string? args = null)
        => async s =>
        {
            try
            {
                GitReflectionService service = GetService();
                string? filter = string.IsNullOrWhiteSpace(args) ? null : args.Trim();
                IReadOnlyList<RepoFileInfo> files = await service.ListSourceFilesAsync(filter);

                StringBuilder sb = new();
                sb.AppendLine($"📁 Source Files{(filter != null ? $" matching '{filter}'" : "")} ({files.Count})");

                foreach (RepoFileInfo file in files.Take(30))
                {
                    sb.AppendLine($"   {file.RelativePath} ({file.LineCount} lines)");
                }

                if (files.Count > 30)
                {
                    sb.AppendLine($"   ... and {files.Count - 30} more");
                }

                Console.WriteLine(sb.ToString());
                s.Output = sb.ToString();
            }
            catch (OperationCanceledException) { throw; }
            catch (IOException ex)
            {
                Console.WriteLine($"[git] Failed to list files: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[git] Failed to list files: {ex.Message}");
            }

            return s;
        };
}
