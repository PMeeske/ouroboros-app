using System.Text.Json;
using Ouroboros.Agent.MetaAI.Affect;
using Ouroboros.Application.Json;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Affective dynamics commands for agent affect monitoring and policy management.
/// Provides show, policy, tune, signal, and reset operations.
/// </summary>
public static class AffectCommands
{
    private static IValenceMonitor? _valenceMonitor;
    private static IHomeostasisPolicy? _homeostasisPolicy;
    private static IPriorityModulator? _priorityModulator;

    /// <summary>
    /// Initializes affect components.
    /// </summary>
    public static void Initialize(
        IValenceMonitor valenceMonitor,
        IHomeostasisPolicy homeostasisPolicy,
        IPriorityModulator priorityModulator)
    {
        _valenceMonitor = valenceMonitor;
        _homeostasisPolicy = homeostasisPolicy;
        _priorityModulator = priorityModulator;
    }

    /// <summary>
    /// Executes an affect command based on the provided options.
    /// </summary>
    public static async Task RunAffectAsync(AffectOptions options)
    {
        try
        {
            // Initialize with default components if not already initialized
            if (_valenceMonitor == null)
            {
                InitializeDefaults();
            }

            string command = options.Command.ToLowerInvariant();
            await (command switch
            {
                "show" => ExecuteShowAsync(options),
                "policy" => ExecutePolicyAsync(options),
                "tune" => ExecuteTuneAsync(options),
                "signal" => ExecuteSignalAsync(options),
                "reset" => ExecuteResetAsync(options),
                _ => PrintErrorAsync($"Unknown affect command: {options.Command}. Valid commands: show, policy, tune, signal, reset")
            });
        }
        catch (InvalidOperationException ex)
        {
            PrintError($"Affect operation failed: {ex.Message}");
            if (options.Verbose)
            {
                AnsiConsole.WriteException(ex);
            }
        }
    }

    private static async Task ExecuteShowAsync(AffectOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Affective State"));
        AnsiConsole.WriteLine();

        AffectiveState state = _valenceMonitor!.GetCurrentState();

        if (options.DetectStress)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("Running FFT stress detection..."));
            AnsiConsole.WriteLine();
            StressDetectionResult stressResult = await _valenceMonitor.DetectStressAsync();
            PrintStressDetection(stressResult);
            AnsiConsole.WriteLine();
        }

        if (options.OutputFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var output = new
            {
                State = state,
                Policy = _homeostasisPolicy!.GetHealthSummary(),
                Queue = _priorityModulator!.GetStatistics()
            };
            string json = JsonSerializer.Serialize(output, JsonDefaults.IndentedExact);
            AnsiConsole.WriteLine(json);

            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                await File.WriteAllTextAsync(options.OutputPath, json);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\n✓ Saved to: {options.OutputPath}"));
            }
        }
        else if (options.OutputFormat.Equals("table", StringComparison.OrdinalIgnoreCase))
        {
            PrintStateTable(state);
            AnsiConsole.WriteLine();
            AnsiConsole.Write(OuroborosTheme.ThemedRule("Homeostasis Policy"));
            PrintPolicyHealth(_homeostasisPolicy!.GetHealthSummary());
            AnsiConsole.WriteLine();
            AnsiConsole.Write(OuroborosTheme.ThemedRule("Priority Queue"));
            PrintQueueStats(_priorityModulator!.GetStatistics());
        }
        else // summary
        {
            PrintStateSummary(state);
        }
    }

    private static Task ExecutePolicyAsync(AffectOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Homeostasis Policies"));
        AnsiConsole.WriteLine();

        List<HomeostasisRule> rules = _homeostasisPolicy!.GetRules(activeOnly: false);

        if (options.OutputFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            string json = JsonSerializer.Serialize(rules, JsonDefaults.IndentedExact);
            AnsiConsole.WriteLine(json);
        }
        else
        {
            var table = OuroborosTheme.ThemedTable("Status", "Priority", "Name", "Signal", "Bounds", "Target", "Action");

            foreach (HomeostasisRule rule in rules)
            {
                string status = rule.IsActive ? "[green]✓[/]" : "[grey]✗[/]";
                table.AddRow(
                    status,
                    $"{rule.Priority:F1}",
                    Markup.Escape(rule.Name),
                    Markup.Escape(rule.TargetSignal.ToString()),
                    $"[{rule.LowerBound:F2}, {rule.UpperBound:F2}]",
                    $"{rule.TargetValue:F2}",
                    Markup.Escape(rule.Action.ToString()));
            }

            AnsiConsole.Write(table);
        }

        // Show recent violations
        List<PolicyViolation> violations = _homeostasisPolicy.GetViolationHistory(5);
        if (violations.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(OuroborosTheme.ThemedRule("Recent Violations"));
            foreach (PolicyViolation violation in violations)
            {
                AnsiConsole.MarkupLine($"  [yellow][[{violation.Severity:P0}]][/] {Markup.Escape(violation.RuleName)}: {Markup.Escape(violation.ViolationType.ToString())}");
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Dim($"Value: {violation.ObservedValue:F3} (bounds: [{violation.LowerBound:F2}, {violation.UpperBound:F2}])")}");
            }
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteTuneAsync(AffectOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RuleName))
        {
            PrintError("Tune command requires --rule parameter");
            return Task.CompletedTask;
        }

        List<HomeostasisRule> rules = _homeostasisPolicy!.GetRules(activeOnly: false);
        HomeostasisRule? rule = rules.FirstOrDefault(r =>
            r.Name.Equals(options.RuleName, StringComparison.OrdinalIgnoreCase));

        if (rule == null)
        {
            PrintError($"Rule not found: {options.RuleName}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("Available rules: " + string.Join(", ", rules.Select(r => r.Name)))}");
            return Task.CompletedTask;
        }

        AnsiConsole.Write(OuroborosTheme.ThemedRule($"Tuning Rule: {rule.Name}"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Current bounds:")} [{rule.LowerBound:F2}, {rule.UpperBound:F2}]");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Current target:")} {rule.TargetValue:F2}");
        AnsiConsole.WriteLine();

        _homeostasisPolicy.UpdateRule(
            rule.Id,
            options.LowerBound,
            options.UpperBound,
            options.TargetValue);

        // Get updated rule
        HomeostasisRule? updated = _homeostasisPolicy.GetRules(activeOnly: false)
            .FirstOrDefault(r => r.Id == rule.Id);

        if (updated != null)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Updated bounds:")} [{updated.LowerBound:F2}, {updated.UpperBound:F2}]");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Updated target:")} {updated.TargetValue:F2}");
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("\n✓ Rule updated successfully"));
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteSignalAsync(AffectOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SignalType))
        {
            PrintError("Signal command requires --type parameter");
            return Task.CompletedTask;
        }

        if (!options.SignalValue.HasValue)
        {
            PrintError("Signal command requires --signal parameter");
            return Task.CompletedTask;
        }

        if (!Enum.TryParse<SignalType>(options.SignalType, true, out SignalType signalType))
        {
            PrintError($"Invalid signal type: {options.SignalType}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("Valid types: stress, confidence, curiosity, valence, arousal")}");
            return Task.CompletedTask;
        }

        AnsiConsole.Write(OuroborosTheme.ThemedRule("Recording Signal"));
        AnsiConsole.WriteLine();

        AffectiveState before = _valenceMonitor!.GetCurrentState();
        _valenceMonitor.RecordSignal("cli", options.SignalValue.Value, signalType);
        AffectiveState after = _valenceMonitor.GetCurrentState();

        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("Signal:")} {signalType} = {options.SignalValue.Value:F3}");
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent("Before:")}");
        PrintStateCompact(before);
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent("After:")}");
        PrintStateCompact(after);

        // Check for policy violations
        List<PolicyViolation> violations = _homeostasisPolicy!.EvaluatePolicies(after);
        if (violations.Any())
        {
            AnsiConsole.MarkupLine($"\n  [yellow]⚠ Policy Violations Detected: {violations.Count}[/]");
            foreach (PolicyViolation violation in violations)
            {
                AnsiConsole.MarkupLine($"    [yellow][[{violation.Severity:P0}]][/] {Markup.Escape(violation.RuleName)}: {Markup.Escape(violation.ViolationType.ToString())}");
            }
        }
        else
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("\n  ✓ All policies within bounds"));
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteResetAsync(AffectOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Resetting Affective State"));
        AnsiConsole.WriteLine();

        AffectiveState before = _valenceMonitor!.GetCurrentState();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Before reset:")}");
        PrintStateCompact(before);

        _valenceMonitor.Reset();

        AffectiveState after = _valenceMonitor.GetCurrentState();
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent("After reset:")}");
        PrintStateCompact(after);

        AnsiConsole.MarkupLine(OuroborosTheme.Ok("\n  ✓ Affective state reset to baseline"));

        return Task.CompletedTask;
    }

    private static void PrintStateTable(AffectiveState state)
    {
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("State ID:")} {state.Id}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Timestamp:")} {state.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine(OuroborosTheme.Accent("  Affective Dimensions:"));
        PrintDimensionBar("Valence", state.Valence, -1.0, 1.0);
        PrintDimensionBar("Stress", state.Stress, 0.0, 1.0);
        PrintDimensionBar("Confidence", state.Confidence, 0.0, 1.0);
        PrintDimensionBar("Curiosity", state.Curiosity, 0.0, 1.0);
        PrintDimensionBar("Arousal", state.Arousal, 0.0, 1.0);
    }

    private static void PrintDimensionBar(string name, double value, double min, double max)
    {
        int barWidth = 30;
        double normalized = (value - min) / (max - min);
        int filled = (int)(normalized * barWidth);
        filled = Math.Clamp(filled, 0, barWidth);

        string bar = new string('█', filled) + new string('░', barWidth - filled);
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent($"{name,-12}")} [[{Markup.Escape(bar)}]] {value:F3}");
    }

    private static void PrintStateCompact(AffectiveState state)
    {
        AnsiConsole.MarkupLine($"    Valence: {state.Valence:F3} | Stress: {state.Stress:F3} | Confidence: {state.Confidence:F3} | Curiosity: {state.Curiosity:F3} | Arousal: {state.Arousal:F3}");
    }

    private static void PrintStateSummary(AffectiveState state)
    {
        string mood = state.Valence switch
        {
            > 0.5 => "very positive",
            > 0.2 => "positive",
            > -0.2 => "neutral",
            > -0.5 => "negative",
            _ => "very negative"
        };

        string stressLevel = state.Stress switch
        {
            > 0.7 => "high stress",
            > 0.4 => "moderate stress",
            _ => "low stress"
        };

        var face = IaretCliAvatar.Inline(state.Valence > 0.2
            ? IaretCliAvatar.Expression.Happy
            : state.Stress > 0.7
                ? IaretCliAvatar.Expression.Concerned
                : IaretCliAvatar.Expression.Idle);

        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText(face)} Agent affect is {Markup.Escape(mood)} with {Markup.Escape(stressLevel)}.");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Confidence:")} {state.Confidence:P0}, {OuroborosTheme.Accent("Curiosity:")} {state.Curiosity:P0}");
    }

    private static void PrintPolicyHealth(PolicyHealthSummary health)
    {
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Rules:")} {health.ActiveRules}/{health.TotalRules} active");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Violations (24h):")} {health.RecentViolations}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Corrections:")} {health.SuccessfulCorrections}/{health.TotalCorrections} successful ({health.CorrectionSuccessRate:P0})");
    }

    private static void PrintQueueStats(QueueStatistics stats)
    {
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Tasks:")} {stats.PendingTasks} pending, {stats.InProgressTasks} in progress");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Completed:")} {stats.CompletedTasks}, Failed: {stats.FailedTasks}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Avg Priority:")} {stats.AverageModulatedPriority:F2}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Max Threat:")} {stats.HighestThreat:F2}, Max Opportunity: {stats.HighestOpportunity:F2}");
    }

    private static void PrintStressDetection(StressDetectionResult result)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("FFT Stress Analysis"));
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Stress Level:")} {result.StressLevel:F3}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Dominant Frequency:")} {result.Frequency:F4} Hz");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Amplitude:")} {result.Amplitude:F4}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Anomalous:")} {(result.IsAnomalous ? "[yellow]⚠ YES[/]" : OuroborosTheme.Ok("✓ No"))}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Spectral Peaks:")} {result.SpectralPeaks.Count}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Analysis:")} {Markup.Escape(result.Analysis)}");
    }

    private static void InitializeDefaults()
    {
        _valenceMonitor = new ValenceMonitor();
        _homeostasisPolicy = new HomeostasisPolicy();
        _priorityModulator = new PriorityModulator();
    }

    private static Task PrintErrorAsync(string message)
    {
        PrintError(message);
        return Task.CompletedTask;
    }

    private static void PrintError(string message)
    {
        var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ {Markup.Escape(message)}[/]");
    }
}
