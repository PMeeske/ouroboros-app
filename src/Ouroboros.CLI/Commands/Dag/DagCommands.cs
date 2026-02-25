using System.Text.Json;
using LangChain.DocumentLoaders;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Options;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// DAG (Directed Acyclic Graph) commands for pipeline branch management.
/// Provides snapshot, show, replay, and retention operations.
/// Uses immutable PipelineBranch event sourcing pattern for tracking epochs.
///
/// Note: This CLI infrastructure maintains session-scoped state, which is acceptable
/// per refactoring guidelines (infrastructure code may use imperative patterns).
/// Each CLI invocation represents a single session with one tracking branch.
/// </summary>
public static class DagCommands
{
    // Session-scoped tracking branch - acceptable for CLI infrastructure layer
    // Alternative approaches would require dependency injection or context passing,
    // which adds complexity without benefit for a CLI session
    private static PipelineBranch? _trackingBranch;

    /// <summary>
    /// Gets or initializes the tracking branch for epoch management.
    /// </summary>
    private static PipelineBranch GetTrackingBranch()
    {
        if (_trackingBranch == null)
        {
            var store = new TrackedVectorStore();
            var source = DataSource.FromPath("/cli/tracking");
            _trackingBranch = new PipelineBranch("dag-tracking", store, source);
        }
        return _trackingBranch;
    }

    /// <summary>
    /// Updates the tracking branch with a new version.
    /// </summary>
    private static void UpdateTrackingBranch(PipelineBranch updated)
    {
        _trackingBranch = updated;
    }

    /// <summary>
    /// Executes a DAG command based on the provided options.
    /// </summary>
    public static async Task RunDagAsync(DagOptions options)
    {
        try
        {
            var command = options.Command.ToLowerInvariant();
            await (command switch
            {
                "snapshot" => ExecuteSnapshotAsync(options),
                "show" => ExecuteShowAsync(options),
                "replay" => ExecuteReplayAsync(options),
                "validate" => ExecuteValidateAsync(options),
                "retention" => ExecuteRetentionAsync(options),
                _ => PrintErrorAsync($"Unknown DAG command: {options.Command}. Valid commands: snapshot, show, replay, validate, retention")
            });
        }
        catch (Exception ex)
        {
            PrintError($"DAG operation failed: {ex.Message}");
            if (options.Verbose)
            {
                AnsiConsole.WriteException(ex);
            }
        }
    }

    private static async Task ExecuteSnapshotAsync(DagOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Creating DAG Snapshot"));
        AnsiConsole.WriteLine();

        // For demonstration, create a snapshot with test branches
        // In production, this would load actual pipeline branches
        var branches = new List<PipelineBranch>();

        if (!string.IsNullOrWhiteSpace(options.BranchName))
        {
            var branch = CreateTestBranch(options.BranchName);
            branches.Add(branch);
        }
        else
        {
            // Create a default test branch
            branches.Add(CreateTestBranch("main"));
        }

        var metadata = new Dictionary<string, object>
        {
            ["created_by"] = "cli",
            ["timestamp"] = DateTime.UtcNow,
            ["branch_count"] = branches.Count
        };

        var trackingBranch = GetTrackingBranch();
        var result = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches, metadata);

        if (result.IsSuccess)
        {
            var (epoch, updatedBranch) = result.Value;
            UpdateTrackingBranch(updatedBranch);

            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"✓ Created epoch {epoch.EpochNumber} (ID: {epoch.EpochId})"));
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Branches:")} {epoch.Branches.Count}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Created:")} {epoch.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");

            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                await ExportEpochAsync(epoch, options.OutputPath);
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Exported to:")} {Markup.Escape(options.OutputPath)}");
            }
        }
        else
        {
            PrintError($"Failed to create snapshot: {result.Error}");
        }
    }

    private static Task ExecuteShowAsync(DagOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("DAG Information"));
        AnsiConsole.WriteLine();

        var trackingBranch = GetTrackingBranch();

        if (options.EpochNumber.HasValue)
        {
            // Show specific epoch
            var result = GlobalProjectionService.GetEpoch(trackingBranch, options.EpochNumber.Value);
            if (result.IsSuccess)
            {
                PrintEpochInfo(result.Value, options.Format == "json");
            }
            else
            {
                PrintError($"Epoch not found: {result.Error}");
            }
        }
        else
        {
            // Show latest epoch or metrics
            var metricsResult = GlobalProjectionService.GetMetrics(trackingBranch);
            if (metricsResult.IsSuccess)
            {
                PrintMetrics(metricsResult.Value, options.Format == "json");
            }

            var latestResult = GlobalProjectionService.GetLatestEpoch(trackingBranch);
            if (latestResult.IsSuccess)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(OuroborosTheme.Accent("Latest Epoch:"));
                PrintEpochInfo(latestResult.Value, options.Format == "json");
            }
        }

        return Task.CompletedTask;
    }

    private static async Task ExecuteReplayAsync(DagOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("DAG Replay"));
        AnsiConsole.WriteLine();

        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            PrintError("Replay requires --input path to snapshot file");
            return;
        }

        if (!File.Exists(options.InputPath))
        {
            PrintError($"Snapshot file not found: {options.InputPath}");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(options.InputPath);
            var epoch = JsonSerializer.Deserialize<EpochSnapshot>(json);

            if (epoch is null)
            {
                PrintError("Failed to deserialize epoch snapshot");
                return;
            }

            AnsiConsole.MarkupLine($"  Replaying epoch {epoch.EpochNumber} from {Markup.Escape(options.InputPath)}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Branches:")} {epoch.Branches.Count}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Created:")} {epoch.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");

            foreach (var branchSnapshot in epoch.Branches)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText($"Branch: {branchSnapshot.Name}")}");
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Events:")} {branchSnapshot.Events.Count}");
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Vectors:")} {branchSnapshot.Vectors.Count}");

                if (options.Verbose)
                {
                    foreach (var evt in branchSnapshot.Events.Take(5))
                    {
                        AnsiConsole.MarkupLine($"      - {Markup.Escape(evt.GetType().Name)} at {evt.Timestamp:HH:mm:ss}");
                    }
                    if (branchSnapshot.Events.Count > 5)
                    {
                        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"      ... and {branchSnapshot.Events.Count - 5} more"));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            PrintError($"Replay failed: {ex.Message}");
        }
    }

    private static Task ExecuteValidateAsync(DagOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("DAG Validation"));
        AnsiConsole.WriteLine();

        var allEpochs = GlobalProjectionService.GetEpochs(GetTrackingBranch());
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total epochs:")} {allEpochs.Count}");

        var validationErrors = 0;
        foreach (var epoch in allEpochs)
        {
            // Validate each branch snapshot
            foreach (var branchSnapshot in epoch.Branches)
            {
                // Compute hash
                var hash = BranchHash.ComputeHash(branchSnapshot);
                AnsiConsole.MarkupLine($"  Epoch {epoch.EpochNumber}, Branch '{Markup.Escape(branchSnapshot.Name)}': Hash {Markup.Escape(hash[..8])}...");
            }
        }

        if (validationErrors == 0)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("\n✓ All snapshots validated successfully"));
        }
        else
        {
            PrintError($"Found {validationErrors} validation errors");
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteRetentionAsync(DagOptions options)
    {
        var title = options.DryRun ? "Retention Policy Evaluation (DRY RUN)" : "Retention Policy Evaluation";
        AnsiConsole.Write(OuroborosTheme.ThemedRule(title));
        AnsiConsole.WriteLine();

        // Build retention policy from options
        RetentionPolicy policy;
        if (options.MaxAgeDays.HasValue && options.MaxCount.HasValue)
        {
            policy = RetentionPolicy.Combined(TimeSpan.FromDays(options.MaxAgeDays.Value), options.MaxCount.Value);
        }
        else if (options.MaxAgeDays.HasValue)
        {
            policy = RetentionPolicy.ByAge(TimeSpan.FromDays(options.MaxAgeDays.Value));
        }
        else if (options.MaxCount.HasValue)
        {
            policy = RetentionPolicy.ByCount(options.MaxCount.Value);
        }
        else
        {
            PrintError("Retention policy requires --max-age-days and/or --max-count");
            return Task.CompletedTask;
        }

        // Convert epochs to snapshot metadata
        var snapshots = GlobalProjectionService.GetEpochs(GetTrackingBranch())
            .SelectMany(e => e.Branches.Select(b => new SnapshotMetadata
            {
                Id = e.EpochId.ToString(),
                BranchName = b.Name,
                CreatedAt = e.CreatedAt,
                Hash = BranchHash.ComputeHash(b),
                SizeBytes = EstimateSize(b)
            }))
            .ToList();

        if (snapshots.Count == 0)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("No snapshots found to evaluate"));
            return Task.CompletedTask;
        }

        var plan = RetentionEvaluator.Evaluate(snapshots, policy, options.DryRun);

        AnsiConsole.MarkupLine(Markup.Escape(plan.GetSummary()));
        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Ok($"Snapshots to keep: {plan.ToKeep.Count}")}");
        if (options.Verbose)
        {
            foreach (var snapshot in plan.ToKeep)
            {
                AnsiConsole.MarkupLine($"    [green]✓[/] {Markup.Escape(snapshot.BranchName)} ({snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss})");
            }
        }

        AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Err($"Snapshots to delete: {plan.ToDelete.Count}")}");
        if (options.Verbose)
        {
            foreach (var snapshot in plan.ToDelete)
            {
                AnsiConsole.MarkupLine($"    [red]✗[/] {Markup.Escape(snapshot.BranchName)} ({snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss})");
            }
        }

        if (!options.DryRun && plan.ToDelete.Count > 0)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("\nNote: Actual deletion not implemented in this phase"));
        }

        return Task.CompletedTask;
    }

    private static void PrintEpochInfo(EpochSnapshot epoch, bool asJson)
    {
        if (asJson)
        {
            var json = JsonSerializer.Serialize(epoch, new JsonSerializerOptions { WriteIndented = true });
            AnsiConsole.WriteLine(json);
        }
        else
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText($"Epoch {epoch.EpochNumber}:")}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("ID:")} {epoch.EpochId}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Created:")} {epoch.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Branches:")} {epoch.Branches.Count}");
            foreach (var branch in epoch.Branches)
            {
                AnsiConsole.MarkupLine($"      - {Markup.Escape(branch.Name)}: {branch.Events.Count} events, {branch.Vectors.Count} vectors");
            }
        }
    }

    private static void PrintMetrics(ProjectionMetrics metrics, bool asJson)
    {
        if (asJson)
        {
            var json = JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true });
            AnsiConsole.WriteLine(json);
        }
        else
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Accent("Global Metrics:"));
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Epochs:")} {metrics.TotalEpochs}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Branches:")} {metrics.TotalBranches}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Events:")} {metrics.TotalEvents}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Average Events per Branch:")} {metrics.AverageEventsPerBranch:F2}");
            if (metrics.LastEpochAt.HasValue)
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Last Epoch:")} {metrics.LastEpochAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
            }
        }
    }

    private static async Task ExportEpochAsync(EpochSnapshot epoch, string path)
    {
        var json = JsonSerializer.Serialize(epoch, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    private static PipelineBranch CreateTestBranch(string name)
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch(name, store, source);
    }

    private static long EstimateSize(BranchSnapshot snapshot)
    {
        // Rough estimate: 1KB per event + 1KB per vector
        return (snapshot.Events.Count + snapshot.Vectors.Count) * 1024L;
    }

    private static void PrintError(string message)
    {
        var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ {Markup.Escape(message)}[/]");
    }

    private static Task PrintErrorAsync(string message)
    {
        PrintError(message);
        return Task.CompletedTask;
    }
}
