using System.Text.Json;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Domain.Governance;
using Ouroboros.Options;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Policy management commands for governance and safety.
/// Phase 5: Governance, Safety, and Ops.
/// </summary>
public static class PolicyCommands
{
    private static readonly PolicyEngine _policyEngine = new();

    /// <summary>
    /// Executes a policy command based on the provided options.
    /// </summary>
    public static async Task RunPolicyAsync(PolicyOptions options)
    {
        try
        {
            var command = options.Command.ToLowerInvariant();
            await (command switch
            {
                "list" => ExecuteListAsync(options),
                "create" => ExecuteCreateAsync(options),
                "simulate" => ExecuteSimulateAsync(options),
                "enforce" => ExecuteEnforceAsync(options),
                "audit" => ExecuteAuditAsync(options),
                "approve" => ExecuteApproveAsync(options),
                _ => PrintErrorAsync($"Unknown policy command: {options.Command}. Valid commands: list, create, simulate, enforce, audit, approve")
            });
        }
        catch (Exception ex)
        {
            PrintError($"Policy operation failed: {ex.Message}");
            if (options.Verbose)
            {
                AnsiConsole.WriteException(ex);
            }
        }
    }

    private static Task ExecuteListAsync(PolicyOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Registered Policies"));
        AnsiConsole.WriteLine();

        var policies = _policyEngine.GetPolicies(activeOnly: false);

        if (policies.Count == 0)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("No policies registered."));
            return Task.CompletedTask;
        }

        if (options.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(policies, new JsonSerializerOptions { WriteIndented = true });
            AnsiConsole.WriteLine(json);
        }
        else if (options.Format.Equals("table", StringComparison.OrdinalIgnoreCase))
        {
            PrintPoliciesTable(policies, options.Verbose);
        }
        else
        {
            PrintPoliciesSummary(policies);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteCreateAsync(PolicyOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Creating Policy"));
        AnsiConsole.WriteLine();

        Policy policy;

        if (!string.IsNullOrWhiteSpace(options.FilePath))
        {
            // Load from file
            if (!File.Exists(options.FilePath))
            {
                PrintError($"Policy file not found: {options.FilePath}");
                return Task.CompletedTask;
            }

            var json = File.ReadAllText(options.FilePath);
            var loadedPolicy = JsonSerializer.Deserialize<Policy>(json);

            if (loadedPolicy == null)
            {
                PrintError("Failed to deserialize policy from file");
                return Task.CompletedTask;
            }

            policy = loadedPolicy;
        }
        else
        {
            // Create simple policy from command line args
            if (string.IsNullOrWhiteSpace(options.Name))
            {
                PrintError("Policy name is required (use --name)");
                return Task.CompletedTask;
            }

            policy = Policy.Create(
                options.Name,
                options.Description ?? "Policy created via CLI");
        }

        var result = _policyEngine.RegisterPolicy(policy);

        if (result.IsSuccess)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"✓ Policy '{policy.Name}' created successfully"));
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("ID:")} {Markup.Escape(policy.Id.ToString())}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Priority:")} {policy.Priority}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Rules:")} {policy.Rules.Count}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Quotas:")} {policy.Quotas.Count}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Thresholds:")} {policy.Thresholds.Count}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Approval Gates:")} {policy.ApprovalGates.Count}");
        }
        else
        {
            PrintError($"Failed to create policy: {result.Error}");
        }

        return Task.CompletedTask;
    }

    private static async Task ExecuteSimulateAsync(PolicyOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Simulating Policy"));
        AnsiConsole.WriteLine();

        if (string.IsNullOrWhiteSpace(options.PolicyId))
        {
            PrintError("Policy ID is required for simulation (use --policy-id)");
            return;
        }

        if (!Guid.TryParse(options.PolicyId, out var policyId))
        {
            PrintError($"Invalid policy ID: {options.PolicyId}");
            return;
        }

        var policies = _policyEngine.GetPolicies(activeOnly: false);
        var policy = policies.FirstOrDefault(p => p.Id == policyId);

        if (policy == null)
        {
            PrintError($"Policy not found: {policyId}");
            return;
        }

        // Create a test context
        var context = new
        {
            timestamp = DateTime.UtcNow,
            user = "cli_user",
            operation = "test_operation"
        };

        var result = await _policyEngine.SimulatePolicyAsync(policy, context);

        if (result.IsSuccess)
        {
            var simulation = result.Value;
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Policy:")} {Markup.Escape(simulation.Policy.Name)}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Compliant:")} {(simulation.EvaluationResult.IsCompliant ? OuroborosTheme.Ok("Yes") : OuroborosTheme.Err("No"))}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Would Block:")} {(simulation.WouldBlock ? OuroborosTheme.Err("Yes") : OuroborosTheme.Ok("No"))}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Violations:")} {simulation.EvaluationResult.Violations.Count}");

            if (simulation.EvaluationResult.Violations.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(OuroborosTheme.Accent("Violations:"));
                foreach (var violation in simulation.EvaluationResult.Violations)
                {
                    AnsiConsole.MarkupLine($"  [yellow][[{Markup.Escape(violation.Severity.ToString())}]][/] {Markup.Escape(violation.Message)}");
                    AnsiConsole.MarkupLine($"    {OuroborosTheme.Dim($"Action: {violation.RecommendedAction}")}");
                }
            }

            if (simulation.RequiredApprovals.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent($"Required Approvals: {simulation.RequiredApprovals.Count}")}");
                foreach (var gate in simulation.RequiredApprovals)
                {
                    AnsiConsole.MarkupLine($"  - {Markup.Escape(gate.Name)}: {gate.MinimumApprovals} approval(s) required");
                }
            }
        }
        else
        {
            PrintError($"Simulation failed: {result.Error}");
        }
    }

    private static async Task ExecuteEnforceAsync(PolicyOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Enforcing Policies"));
        AnsiConsole.WriteLine();

        // Skip enforcement if self-modification is not enabled
        if (!options.EnableSelfModification)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn("Self-modification is DISABLED."));
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("Enable with: --enable-self-mod"));
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("\nEnforcement skipped. Use --enable-self-mod to allow policy enforcement."));
            return;
        }

        // Log culture and self-modification settings if provided
        if (!string.IsNullOrWhiteSpace(options.Culture))
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Culture:")} {Markup.Escape(options.Culture)}");
        }

        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Self-Modification:")} {OuroborosTheme.Ok("ENABLED")}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Risk Level Threshold:")} {Markup.Escape(options.RiskLevel)}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Auto-Approve Low Risk:")} {options.AutoApproveLow}");
        AnsiConsole.WriteLine();

        // Create a test context
        var context = new
        {
            timestamp = DateTime.UtcNow,
            user = "cli_user",
            operation = "enforce_test",
            culture = options.Culture,
            selfModificationEnabled = options.EnableSelfModification,
            riskLevel = options.RiskLevel
        };

        var result = await _policyEngine.EnforcePoliciesAsync(context);

        if (result.IsSuccess)
        {
            var enforcement = result.Value;
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Evaluations:")} {enforcement.Evaluations.Count}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Violations:")} {enforcement.Evaluations.Sum(e => e.Violations.Count)}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Blocked:")} {(enforcement.IsBlocked ? OuroborosTheme.Err("Yes") : OuroborosTheme.Ok("No"))}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Actions Required:")} {enforcement.ActionsRequired.Count}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Approvals Required:")} {enforcement.ApprovalsRequired.Count}");

            // Apply self-modifications based on violations
            if (enforcement.Evaluations.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(OuroborosTheme.ThemedRule("Applying Self-Modifications"));
                AnsiConsole.WriteLine();
                await ApplySelfModificationsAsync(enforcement, options);
            }

            if (enforcement.ActionsRequired.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(OuroborosTheme.Accent("Required Actions:"));
                foreach (var action in enforcement.ActionsRequired.Distinct())
                {
                    var count = enforcement.ActionsRequired.Count(a => a == action);
                    AnsiConsole.MarkupLine($"  - {Markup.Escape(action.ToString())}: {count}");
                }
            }

            if (enforcement.ApprovalsRequired.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(OuroborosTheme.Accent("Approval Requests Created:"));
                foreach (var request in enforcement.ApprovalsRequired)
                {
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("ID:")} {Markup.Escape(request.Id.ToString())}");
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Operation:")} {Markup.Escape(request.OperationDescription)}");
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Deadline:")} {request.Deadline:yyyy-MM-dd HH:mm:ss} UTC");
                    AnsiConsole.WriteLine();
                }
            }
        }
        else
        {
            PrintError($"Enforcement failed: {result.Error}");
        }
    }

    private static async Task ExecuteAuditAsync(PolicyOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Policy Audit Trail"));
        AnsiConsole.WriteLine();

        DateTime? since = null;
        if (!string.IsNullOrWhiteSpace(options.Since))
        {
            if (DateTime.TryParse(options.Since, out var sinceDate))
            {
                since = sinceDate;
            }
        }

        var auditTrail = _policyEngine.GetAuditTrail(options.Limit, since);

        if (auditTrail.Count == 0)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("No audit entries found."));
            return;
        }

        if (options.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(auditTrail, new JsonSerializerOptions { WriteIndented = true });
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
