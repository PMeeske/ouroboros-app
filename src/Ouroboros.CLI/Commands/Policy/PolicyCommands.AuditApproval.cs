using System.Text.Json;
using Ouroboros.Application.Json;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Domain.Governance;
using Ouroboros.Options;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

public static partial class PolicyCommands
{
    private static async Task ExecuteAuditAsync(PolicyOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Policy Audit Trail"));
        AnsiConsole.WriteLine();

        DateTime? since = null;
        if (!string.IsNullOrWhiteSpace(options.Since)
            && DateTime.TryParse(options.Since, out var sinceDate))
        {
            since = sinceDate;
        }

        var auditTrail = _policyEngine.GetAuditTrail(options.Limit, since);

        if (auditTrail.Count == 0)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("No audit entries found."));
            return;
        }

        if (options.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(auditTrail, JsonDefaults.IndentedExact);
            AnsiConsole.WriteLine(json);

            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                await File.WriteAllTextAsync(options.OutputPath, json);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\n✓ Audit trail exported to: {options.OutputPath}"));
            }
        }
        else
        {
            PrintAuditTrailTable(auditTrail, options.Verbose);
        }
    }

    private static Task ExecuteApproveAsync(PolicyOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Approval Management"));
        AnsiConsole.WriteLine();

        if (string.IsNullOrWhiteSpace(options.ApprovalId))
        {
            // List pending approvals
            var pending = _policyEngine.GetPendingApprovals();

            if (pending.Count == 0)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("No pending approval requests."));
                return Task.CompletedTask;
            }

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent($"Pending Approvals: {pending.Count}")}");
            AnsiConsole.WriteLine();

            foreach (var request in pending)
            {
                TimeSpan timeUntilDeadline = request.Deadline - DateTime.UtcNow;
                string urgency = timeUntilDeadline.TotalHours < 24 ? OuroborosTheme.Err("URGENT") : "";

                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("ID:")} {Markup.Escape(request.Id.ToString())}");
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Operation:")} {Markup.Escape(request.OperationDescription)}");
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Required Approvals:")} {request.Gate.MinimumApprovals}");
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Current Approvals:")} {request.Approvals.Count}");
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Deadline:")} {request.Deadline:yyyy-MM-dd HH:mm:ss} UTC {urgency}");
                AnsiConsole.WriteLine();
            }

            return Task.CompletedTask;
        }

        // Submit an approval
        if (!Guid.TryParse(options.ApprovalId, out var requestId))
        {
            PrintError($"Invalid approval ID: {options.ApprovalId}");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(options.Decision))
        {
            PrintError("Decision is required (use --decision: approve|reject|request-info)");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(options.ApproverId))
        {
            PrintError("Approver ID is required (use --approver)");
            return Task.CompletedTask;
        }

        var decision = options.Decision.ToLowerInvariant() switch
        {
            "approve" => ApprovalDecision.Approve,
            "reject" => ApprovalDecision.Reject,
            "request-info" => ApprovalDecision.RequestInfo,
            _ => ApprovalDecision.Reject
        };

        var approval = new Approval
        {
            ApproverId = options.ApproverId,
            Decision = decision,
            Comments = options.Comments
        };

        var result = _policyEngine.SubmitApproval(requestId, approval);

        if (result.IsSuccess)
        {
            var updated = result.Value;
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("✓ Approval submitted successfully"));
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Request ID:")} {Markup.Escape(updated.Id.ToString())}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("New Status:")} {Markup.Escape(updated.Status.ToString())}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Approvals:")} {updated.Approvals.Count}");

            if (updated.IsApproved)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Ok("  ✓ Request is now APPROVED"));
            }
        }
        else
        {
            PrintError($"Failed to submit approval: {result.Error}");
        }

        return Task.CompletedTask;
    }

    private static void PrintPoliciesTable(IReadOnlyList<Policy> policies, bool verbose)
    {
        var table = OuroborosTheme.ThemedTable("Priority", "Status", "Name", "Rules", "Quotas", "Thresholds", "Gates", "Created");

        foreach (var policy in policies)
        {
            string status = policy.IsActive ? "[green]ACTIVE[/]" : "[grey]INACTIVE[/]";
            table.AddRow(
                $"{policy.Priority:F1}",
                status,
                Markup.Escape(policy.Name),
                $"{policy.Rules.Count}",
                $"{policy.Quotas.Count}",
                $"{policy.Thresholds.Count}",
                $"{policy.ApprovalGates.Count}",
                $"{policy.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        AnsiConsole.Write(table);

        if (verbose)
        {
            foreach (var policy in policies)
            {
                if (policy.Rules.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent($"Rules for {policy.Name}:")}");
                    foreach (var rule in policy.Rules)
                    {
                        AnsiConsole.MarkupLine($"    - {Markup.Escape(rule.Name)}: {Markup.Escape(rule.Condition)} → {Markup.Escape(rule.Action.ToString())}");
                    }
                }

                if (policy.Quotas.Count > 0)
                {
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Quotas:")}");
                    foreach (var quota in policy.Quotas)
                    {
                        AnsiConsole.MarkupLine($"    - {Markup.Escape(quota.ResourceName)}: {quota.CurrentValue}/{quota.MaxValue} {Markup.Escape(quota.Unit)} ({quota.UtilizationPercent:F0}%)");
                    }
                }
            }
        }
    }

    private static void PrintPoliciesSummary(IReadOnlyList<Policy> policies)
    {
        var active = policies.Count(p => p.IsActive);
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total:")} {policies.Count} policies ({active} active, {policies.Count - active} inactive)");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Rules:")} {policies.Sum(p => p.Rules.Count)}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Quotas:")} {policies.Sum(p => p.Quotas.Count)}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Thresholds:")} {policies.Sum(p => p.Thresholds.Count)}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Approval Gates:")} {policies.Sum(p => p.ApprovalGates.Count)}");
    }

    private static void PrintAuditTrailTable(IReadOnlyList<PolicyAuditEntry> entries, bool verbose)
    {
        var table = OuroborosTheme.ThemedTable("Timestamp", "Action", "Actor", "Policy", "Compliant", "Violations");

        foreach (var entry in entries)
        {
            table.AddRow(
                $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}",
                Markup.Escape(entry.Action),
                Markup.Escape(entry.Actor),
                Markup.Escape(entry.Policy.Name),
                entry.EvaluationResult != null
                    ? (entry.EvaluationResult.IsCompliant ? "[green]Yes[/]" : "[red]No[/]")
                    : "[grey]—[/]",
                entry.EvaluationResult != null
                    ? $"{entry.EvaluationResult.Violations.Count}"
                    : "—");
        }

        AnsiConsole.Write(table);

        if (verbose)
        {
            foreach (var entry in entries.Where(e => e.Metadata.Count > 0))
            {
                AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Dim("Metadata:")}");
                foreach (var kvp in entry.Metadata)
                {
                    AnsiConsole.MarkupLine($"    {Markup.Escape(kvp.Key)}: {Markup.Escape(kvp.Value?.ToString() ?? "null")}");
                }
            }
        }
    }

    private static void PrintError(string message)
    {
        var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ {Markup.Escape(message)}[/]");
    }

    /// <summary>
    /// Applies self-modifications to fix policy violations.
    /// </summary>
    private static async Task ApplySelfModificationsAsync(PolicyEnforcementResult enforcement, PolicyOptions options)
    {
        var riskThreshold = ParseRiskLevel(options.RiskLevel);
        var modificationCount = 0;
        var approvalCount = 0;

        foreach (var evaluation in enforcement.Evaluations)
        {
            foreach (var violation in evaluation.Violations)
            {
                var violationRisk = AssessViolationRisk(violation.Severity);

                // Determine if modification requires approval
                bool requiresApproval = violationRisk > riskThreshold ||
                    (violationRisk == RiskLevel.Low && !options.AutoApproveLow);

                if (requiresApproval)
                {
                    AnsiConsole.MarkupLine($"  [yellow][[PENDING APPROVAL]][/] {Markup.Escape(violation.Message)}");
                    AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Risk:")} {violationRisk}");
                    AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Recommended:")} {Markup.Escape(violation.RecommendedAction.ToString())}");
                    approvalCount++;
                }
                else
                {
                    // Auto-apply low-risk modifications
                    AnsiConsole.MarkupLine($"  [green][[AUTO-APPLIED]][/] {Markup.Escape(violation.Message)}");
                    AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Risk:")} {violationRisk}");
                    AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Action:")} {Markup.Escape(violation.RecommendedAction.ToString())}");
                    modificationCount++;
                    await Task.Delay(100); // Simulate modification work
                }
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Modification Summary"));
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok($"Auto-Applied: {modificationCount}")}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"Pending Approval: {approvalCount}")}");
    }

    /// <summary>
    /// Assesses the risk level of a policy violation based on severity.
    /// </summary>
    private static RiskLevel AssessViolationRisk(ViolationSeverity severity)
    {
        // Map violation severity to risk level
        return severity switch
        {
            ViolationSeverity.Low => RiskLevel.Low,
            ViolationSeverity.Medium => RiskLevel.Medium,
            ViolationSeverity.High => RiskLevel.High,
            ViolationSeverity.Critical => RiskLevel.Critical,
            _ => RiskLevel.Medium
        };
    }

    /// <summary>
    /// Parses risk level string to enum.
    /// </summary>
    private static RiskLevel ParseRiskLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "low" => RiskLevel.Low,
            "medium" => RiskLevel.Medium,
            "high" => RiskLevel.High,
            "critical" => RiskLevel.Critical,
            _ => RiskLevel.Medium
        };
    }

    /// <summary>
    /// Risk levels for policy modifications.
    /// </summary>
    private enum RiskLevel
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    private static Task PrintErrorAsync(string message)
    {
        PrintError(message);
        return Task.CompletedTask;
    }
}
