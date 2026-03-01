using System.Text.Json;
using Ouroboros.Application.Json;
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
public static partial class PolicyCommands
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
        catch (InvalidOperationException ex)
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
            var json = JsonSerializer.Serialize(policies, JsonDefaults.IndentedExact);
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

}
