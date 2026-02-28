// <copyright file="GitReflectionCliSteps.Git.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using Ouroboros.Application.Tools;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application;

/// <summary>
/// Git operation pipeline steps: status, branch, commit, log.
/// </summary>
public static partial class GitReflectionCliSteps
{
    /// <summary>
    /// Get Git status.
    /// Usage: GitStatus()
    /// </summary>
    [PipelineToken("GitStatus", "Status")]
    public static Step<CliPipelineState, CliPipelineState> GitStatus(string? args = null)
        => async s =>
        {
            try
            {
                GitReflectionService service = GetService();
                string status = await service.GetStatusAsync();
                string branch = await service.GetCurrentBranchAsync();

                Console.WriteLine($"🌿 Branch: {branch}");
                Console.WriteLine(status);

                s.Output = $"Branch: {branch}\n{status}";
                s.Context = $"On branch {branch}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Status failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Create a new branch for self-modification.
    /// Usage: GitBranch('feature-name')
    /// </summary>
    [PipelineToken("GitBranch", "CreateBranch")]
    public static Step<CliPipelineState, CliPipelineState> GitBranch(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: GitBranch('branch-name')");
                return s;
            }

            try
            {
                GitReflectionService service = GetService();
                GitOperationResult result = await service.CreateBranchAsync(args.Trim());

                if (result.Success)
                {
                    Console.WriteLine($"✅ {result.Message}");
                    s.Context = $"On branch {result.BranchName}";
                }
                else
                {
                    Console.WriteLine($"❌ {result.Message}");
                }

                s.Output = result.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Branch creation failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Commit staged changes.
    /// Usage: GitCommit('commit message')
    /// </summary>
    [PipelineToken("GitCommit", "Commit")]
    public static Step<CliPipelineState, CliPipelineState> GitCommit(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: GitCommit('commit message')");
                return s;
            }

            try
            {
                GitReflectionService service = GetService();
                GitOperationResult result = await service.CommitAsync(args.Trim());

                if (result.Success)
                {
                    Console.WriteLine($"✅ {result.Message}");
                    Console.WriteLine($"   Commit: {result.CommitHash}");
                }
                else
                {
                    Console.WriteLine($"❌ {result.Message}");
                }

                s.Output = result.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Commit failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Get recent commits.
    /// Usage: GitLog()
    /// Usage: GitLog('20') - get 20 commits
    /// </summary>
    [PipelineToken("GitLog", "RecentCommits")]
    public static Step<CliPipelineState, CliPipelineState> GitLog(string? args = null)
        => async s =>
        {
            try
            {
                int count = 10;
                if (!string.IsNullOrWhiteSpace(args) && int.TryParse(args.Trim(), out int parsed))
                {
                    count = Math.Min(50, Math.Max(1, parsed));
                }

                GitReflectionService service = GetService();
                var commits = await service.GetRecentCommitsAsync(count);

                StringBuilder sb = new();
                sb.AppendLine($"📜 Recent Commits ({commits.Count}):");

                foreach (var commit in commits)
                {
                    sb.AppendLine($"   {commit.Hash} - {commit.Message} ({commit.Timestamp:yyyy-MM-dd})");
                }

                Console.WriteLine(sb.ToString());
                s.Output = sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Log failed: {ex.Message}");
            }

            return s;
        };
}
