// <copyright file="GitReflectionCliSteps.SelfModification.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using Ouroboros.Application.Tools;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application;

/// <summary>
/// Self-modification pipeline steps: propose, approve, apply, view, self-modify, and reflect.
/// </summary>
public static partial class GitReflectionCliSteps
{
    /// <summary>
    /// Propose a code change for review.
    /// Usage: ProposeChange('file;description;old_code;new_code')
    /// </summary>
    [PipelineToken("ProposeChange", "Propose")]
    public static Step<CliPipelineState, CliPipelineState> ProposeChange(string? args = null)
        => s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: ProposeChange('file;description;old_code;new_code')");
                Console.WriteLine("[git] Or set s.Context with proposal details and use ProposeFromContext()");
                return Task.FromResult(s);
            }

            try
            {
                string[] parts = args.Split(';');
                if (parts.Length < 4)
                {
                    Console.WriteLine("[git] Need at least 4 parts: file;description;old_code;new_code");
                    return Task.FromResult(s);
                }

                GitReflectionService service = GetService();
                CodeChangeProposal proposal = service.ProposeChange(
                    parts[0].Trim(),
                    parts[1].Trim(),
                    "Self-improvement via pipeline",
                    parts[2].Trim(),
                    parts[3].Trim(),
                    ChangeCategory.Refactoring,
                    RiskLevel.Medium);

                Console.WriteLine($"📝 Proposal created: {proposal.Id}");
                Console.WriteLine($"   File: {proposal.FilePath}");
                Console.WriteLine($"   Risk: {proposal.Risk}");
                Console.WriteLine($"   Use ApproveChange('{proposal.Id}') to approve");

                s.Context = proposal.Id;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"[git] Proposal failed: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[git] Proposal failed: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Approve a pending change proposal.
    /// Usage: ApproveChange('proposal_id')
    /// </summary>
    [PipelineToken("ApproveChange", "Approve")]
    public static Step<CliPipelineState, CliPipelineState> ApproveChange(string? args = null)
        => s =>
        {
            string proposalId = args?.Trim() ?? s.Context;
            if (string.IsNullOrWhiteSpace(proposalId))
            {
                Console.WriteLine("[git] Usage: ApproveChange('proposal_id')");
                return Task.FromResult(s);
            }

            try
            {
                GitReflectionService service = GetService();
                bool success = service.ApproveProposal(proposalId);

                if (success)
                {
                    Console.WriteLine($"✅ Proposal {proposalId} approved");
                    Console.WriteLine($"   Use ApplyChange('{proposalId}') to apply");
                }
                else
                {
                    Console.WriteLine($"❌ Proposal {proposalId} not found");
                }

                s.Context = proposalId;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[git] Approval failed: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Apply an approved change.
    /// Usage: ApplyChange('proposal_id')
    /// Usage: ApplyChange('proposal_id;commit') - auto-commit after applying
    /// </summary>
    [PipelineToken("ApplyChange", "Apply")]
    public static Step<CliPipelineState, CliPipelineState> ApplyChange(string? args = null)
        => async s =>
        {
            string proposalId = args?.Split(';')[0].Trim() ?? s.Context;
            bool autoCommit = args?.Contains("commit") == true;

            if (string.IsNullOrWhiteSpace(proposalId))
            {
                Console.WriteLine("[git] Usage: ApplyChange('proposal_id') or ApplyChange('proposal_id;commit')");
                return s;
            }

            try
            {
                GitReflectionService service = GetService();
                GitOperationResult result = await service.ApplyProposalAsync(proposalId, autoCommit);

                if (result.Success)
                {
                    Console.WriteLine($"✅ {result.Message}");
                    Console.WriteLine("⚠️  Run `dotnet build` to verify changes");
                }
                else
                {
                    Console.WriteLine($"❌ {result.Message}");
                }

                s.Output = result.Message;
            }
            catch (OperationCanceledException) { throw; }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[git] Apply failed: {ex.Message}");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[git] Apply failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// View all change proposals and their status.
    /// Usage: ViewProposals()
    /// </summary>
    [PipelineToken("ViewProposals", "Proposals")]
    public static Step<CliPipelineState, CliPipelineState> ViewProposals(string? args = null)
        => s =>
        {
            GitReflectionService service = GetService();
            string summary = service.GetModificationSummary();

            Console.WriteLine(summary);
            s.Output = summary;

            return Task.FromResult(s);
        };

    /// <summary>
    /// Complete self-modification workflow: analyze, propose, approve, apply.
    /// Usage: SelfModify('file;description;old_code;new_code')
    /// </summary>
    [PipelineToken("SelfModify", "ModifyMyself")]
    public static Step<CliPipelineState, CliPipelineState> SelfModify(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: SelfModify('file;description;old_code;new_code')");
                return s;
            }

            try
            {
                string[] parts = args.Split(';');
                if (parts.Length < 4)
                {
                    Console.WriteLine("[git] Need at least 4 parts: file;description;old_code;new_code");
                    return s;
                }

                GitReflectionService service = GetService();
                GitOperationResult result = await service.SelfModifyAsync(
                    parts[0].Trim(),
                    parts[1].Trim(),
                    "Self-improvement via pipeline DSL",
                    parts[2].Trim(),
                    parts[3].Trim(),
                    ChangeCategory.Refactoring,
                    autoApprove: true);

                if (result.Success)
                {
                    Console.WriteLine("🧬 Self-Modification Complete");
                    Console.WriteLine($"   {result.Message}");
                    Console.WriteLine("⚠️  Run `dotnet build` to verify changes");
                }
                else
                {
                    Console.WriteLine($"❌ {result.Message}");
                }

                s.Output = result.Message;
            }
            catch (OperationCanceledException) { throw; }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[git] Self-modification failed: {ex.Message}");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[git] Self-modification failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Reflect on a file and suggest improvements.
    /// Usage: ReflectOnFile('path/to/file.cs')
    /// </summary>
    [PipelineToken("ReflectOnFile", "CodeReflect")]
    public static Step<CliPipelineState, CliPipelineState> ReflectOnFile(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: ReflectOnFile('path/to/file.cs')");
                return s;
            }

            try
            {
                GitReflectionService service = GetService();
                CodeAnalysis analysis = await service.AnalyzeFileAsync(args.Trim());

                Console.WriteLine($"\n🔍 Self-Reflection: {analysis.FilePath}");
                Console.WriteLine($"════════════════════════════════════════════");

                // Summary
                Console.WriteLine($"📊 Metrics:");
                Console.WriteLine($"   Classes: {analysis.Classes.Count}");
                Console.WriteLine($"   Methods: {analysis.Methods.Count}");
                Console.WriteLine($"   Lines: {analysis.TotalLines} ({analysis.CommentRatio:P0} comments)");

                // Issues
                if (analysis.PotentialIssues.Count > 0)
                {
                    Console.WriteLine($"\n⚠️  Issues ({analysis.PotentialIssues.Count}):");
                    foreach (string issue in analysis.PotentialIssues)
                    {
                        Console.WriteLine($"   - {issue}");
                    }
                }

                // TODOs
                if (analysis.Todos.Count > 0)
                {
                    Console.WriteLine($"\n📝 TODOs ({analysis.Todos.Count}):");
                    foreach (string todo in analysis.Todos.Take(5))
                    {
                        Console.WriteLine($"   - {todo}");
                    }
                }

                // Suggestions
                Console.WriteLine($"\n💡 Improvement Suggestions:");
                if (analysis.CommentRatio < 0.1)
                {
                    Console.WriteLine("   - Add more documentation (comment ratio < 10%)");
                }
                if (analysis.Methods.Count > 20)
                {
                    Console.WriteLine($"   - Consider splitting file ({analysis.Methods.Count} methods is large)");
                }
                if (analysis.TotalLines > 500)
                {
                    Console.WriteLine($"   - File is large ({analysis.TotalLines} lines), consider refactoring");
                }

                Console.WriteLine($"\nUse SelfModify() to apply improvements");

                s.Output = $"Reflected on {analysis.FilePath}";
                s.Context = args.Trim();
            }
            catch (OperationCanceledException) { throw; }
            catch (IOException ex)
            {
                Console.WriteLine($"[git] Reflection failed: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[git] Reflection failed: {ex.Message}");
            }

            return s;
        };
}
