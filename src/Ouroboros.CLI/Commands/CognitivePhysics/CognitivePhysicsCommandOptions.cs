using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Options for the cognitive-physics command using System.CommandLine 2.0.3 GA.
/// Exposes ZeroShift, trajectory, superposition, chaos, and adaptation operations.
/// </summary>
public class CognitivePhysicsCommandOptions
{
    // ── Operation Mode ──────────────────────────────────────────────────

    public System.CommandLine.Option<string> OperationOption { get; } = new("--operation")
    {
        Description = "CPE operation: shift | trajectory | entangle | collapse | chaos",
        DefaultValueFactory = _ => "shift"
    };

    // ── Domain / Focus ──────────────────────────────────────────────────

    public System.CommandLine.Option<string> FocusOption { get; } = new("--focus")
    {
        Description = "Initial conceptual domain (starting focus)",
        DefaultValueFactory = _ => "general"
    };

    public System.CommandLine.Option<string> TargetOption { get; } = new("--target")
    {
        Description = "Target conceptual domain for shift operation"
    };

    public System.CommandLine.Option<string[]> TargetsOption { get; } = new("--targets")
    {
        Description = "Ordered list of target domains for trajectory/entangle operations",
        AllowMultipleArgumentsPerToken = true
    };

    // ── Resources & Budget ──────────────────────────────────────────────

    public System.CommandLine.Option<double> ResourcesOption { get; } = new("--resources")
    {
        Description = "Initial cognitive resource budget",
        DefaultValueFactory = _ => 100.0
    };

    // ── Chaos Configuration ─────────────────────────────────────────────

    public System.CommandLine.Option<double> ChaosIntensityOption { get; } = new("--chaos-intensity")
    {
        Description = "Chaos injection intensity (0.0–1.0)",
        DefaultValueFactory = _ => 0.1
    };

    public System.CommandLine.Option<double> ChaosResourceCostOption { get; } = new("--chaos-cost")
    {
        Description = "Resource cost per chaos injection",
        DefaultValueFactory = _ => 5.0
    };

    // ── Evolution Configuration ─────────────────────────────────────────

    public System.CommandLine.Option<double> EvolutionSuccessRateOption { get; } = new("--evolution-success-rate")
    {
        Description = "Compression improvement rate on success",
        DefaultValueFactory = _ => 0.05
    };

    public System.CommandLine.Option<double> EvolutionFailureRateOption { get; } = new("--evolution-failure-rate")
    {
        Description = "Compression degradation rate on failure",
        DefaultValueFactory = _ => 0.1
    };

    // ── Output ──────────────────────────────────────────────────────────

    public System.CommandLine.Option<bool> JsonOutputOption { get; } = new("--json")
    {
        Description = "Output results as JSON",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> VerboseOption { get; } = new("--verbose")
    {
        Description = "Show detailed state transitions",
        DefaultValueFactory = _ => false
    };

    /// <summary>
    /// Adds all cognitive-physics options to the given command.
    /// </summary>
    public void AddToCommand(Command command)
    {
        command.Add(OperationOption);
        command.Add(FocusOption);
        command.Add(TargetOption);
        command.Add(TargetsOption);
        command.Add(ResourcesOption);
        command.Add(ChaosIntensityOption);
        command.Add(ChaosResourceCostOption);
        command.Add(EvolutionSuccessRateOption);
        command.Add(EvolutionFailureRateOption);
        command.Add(JsonOutputOption);
        command.Add(VerboseOption);
    }
}
