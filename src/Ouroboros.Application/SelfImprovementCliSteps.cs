#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Self-Improvement CLI Steps
// DSL tokens for autonomous learning, capability tracking, and self-execution
// ==========================================================

using System.Text;

namespace Ouroboros.Application;

/// <summary>
/// DSL steps for self-improvement, capability tracking, and autonomous execution.
/// </summary>
public static partial class SelfImprovementCliSteps
{
    // ═══════════════════════════════════════════════════════════════════════════
    // CAPABILITY TRACKING STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tracks capability usage during pipeline execution.
    /// Enables self-improvement by monitoring success/failure rates.
    /// Usage: TrackCapability('planning') | ...rest of pipeline...
    /// </summary>
    [PipelineToken("TrackCapability", "Capability")]
    public static Step<CliPipelineState, CliPipelineState> TrackCapability(string? args = null)
    {
        var capabilityName = CliSteps.ParseString(args ?? "general");
        return TrackedStep.Wrap(
            async s =>
            {
                var startTime = DateTime.UtcNow;

                // Store tracking info in branch metadata
                s.Branch = s.Branch.WithIngestEvent(
                    $"capability:start:{capabilityName}",
                    new[] { startTime.ToString("O") });

                if (s.Trace)
                {
                    Console.WriteLine($"[trace] Tracking capability: {capabilityName}");
                }

                return s;
            },
            "TrackCapability",
            new[] { "Capability" },
            nameof(SelfImprovementCliSteps),
            "Tracks capability usage for self-improvement",
            args);
    }

    /// <summary>
    /// Completes capability tracking and records success/failure.
    /// Usage: ...pipeline... | EndCapability('success') or EndCapability('failure')
    /// </summary>
    [PipelineToken("EndCapability", "CompleteCapability")]
    public static Step<CliPipelineState, CliPipelineState> EndCapability(string? args = null)
    {
        var success = args?.ToLowerInvariant().Contains("success") ?? true;
        return TrackedStep.Wrap(
            async s =>
            {
                var endTime = DateTime.UtcNow;

                s.Branch = s.Branch.WithIngestEvent(
                    $"capability:end:{(success ? "success" : "failure")}",
                    new[] { endTime.ToString("O") });

                if (s.Trace)
                {
                    Console.WriteLine($"[trace] Capability tracking completed: {(success ? "SUCCESS" : "FAILURE")}");
                }

                return s;
            },
            "EndCapability",
            new[] { "CompleteCapability" },
            nameof(SelfImprovementCliSteps),
            "Completes capability tracking",
            args);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // REIFICATION STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Enables full network state reification for the pipeline.
    /// All subsequent steps will be tracked in the MerkleDag.
    /// Usage: Reify | ...rest of pipeline...
    /// </summary>
    [PipelineToken("Reify", "EnableReification", "TrackNetwork")]
    public static Step<CliPipelineState, CliPipelineState> Reify(string? args = null)
        => TrackedStep.Wrap(
            async s =>
            {
                // Enable network tracking if not already active
                s = s.WithNetworkTracking();

                if (s.Trace)
                {
                    Console.WriteLine("[trace] Network reification enabled - all steps will be tracked in MerkleDag");
                }

                return s;
            },
            "Reify",
            new[] { "EnableReification", "TrackNetwork" },
            nameof(SelfImprovementCliSteps),
            "Enables network state reification",
            args);

    /// <summary>
    /// Creates a checkpoint in the network state for replay/debugging.
    /// Usage: Checkpoint('milestone-name')
    /// </summary>
    [PipelineToken("Checkpoint", "SaveState")]
    public static Step<CliPipelineState, CliPipelineState> Checkpoint(string? args = null)
    {
        var checkpointName = CliSteps.ParseString(args ?? $"checkpoint-{DateTime.UtcNow:HHmmss}");
        return TrackedStep.Wrap(
            async s =>
            {
                // Update network state with checkpoint
                if (s.NetworkTracker != null)
                {
                    var snapshot = s.NetworkTracker.CreateSnapshot();
                    s.Branch = s.Branch.WithIngestEvent(
                        $"checkpoint:{checkpointName}",
                        new[] { snapshot.TotalNodes.ToString(), snapshot.TotalTransitions.ToString() });
                }

                if (s.Trace)
                {
                    Console.WriteLine($"[trace] Checkpoint created: {checkpointName}");
                }

                return s;
            },
            "Checkpoint",
            new[] { "SaveState" },
            nameof(SelfImprovementCliSteps),
            "Creates a checkpoint in the network state",
            args);
    }

    /// <summary>
    /// Shows the current network state summary.
    /// Usage: NetworkStatus
    /// </summary>
    [PipelineToken("NetworkStatus", "DagStatus")]
    public static Step<CliPipelineState, CliPipelineState> NetworkStatus(string? args = null)
        => TrackedStep.Wrap(
            async s =>
            {
                var summary = s.GetNetworkStateSummary();

                if (summary != null)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\n=== Network State ===");
                    Console.WriteLine(summary);
                    Console.WriteLine("====================\n");
                    Console.ResetColor();

                    s.Output = summary;
                }
                else
                {
                    Console.WriteLine("[info] Network tracking not enabled. Use 'Reify' first.");
                }

                return s;
            },
            "NetworkStatus",
            new[] { "DagStatus" },
            nameof(SelfImprovementCliSteps),
            "Shows the current network state summary",
            args);
}
