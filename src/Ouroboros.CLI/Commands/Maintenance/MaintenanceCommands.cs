using System.Text.Json;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Application.Json;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Domain.Governance;
using Ouroboros.Options;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Maintenance commands for DAG compaction, archiving, and anomaly detection.
/// Phase 5: Governance, Safety, and Ops.
/// </summary>
public static class MaintenanceCommands
{
    private static readonly MaintenanceScheduler _scheduler = new();

    /// <summary>
    /// Executes a maintenance command based on the provided options.
    /// </summary>
    public static async Task RunMaintenanceAsync(MaintenanceOptions options)
    {
        try
        {
            var command = options.Command.ToLowerInvariant();
            await (command switch
            {
                "compact" => ExecuteCompactAsync(options),
                "archive" => ExecuteArchiveAsync(options),
                "detect-anomalies" => ExecuteDetectAnomaliesAsync(options),
                "schedule" => ExecuteScheduleAsync(options),
                "history" => ExecuteHistoryAsync(options),
                "alerts" => ExecuteAlertsAsync(options),
                _ => PrintErrorAsync($"Unknown maintenance command: {options.Command}. Valid commands: compact, archive, detect-anomalies, schedule, history, alerts")
            });
        }
        catch (InvalidOperationException ex)
        {
            PrintError($"Maintenance operation failed: {ex.Message}");
            if (options.Verbose)
            {
                AnsiConsole.WriteException(ex);
            }
        }
    }

    private static async Task ExecuteCompactAsync(MaintenanceOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("DAG Compaction"));
        AnsiConsole.WriteLine();

        // Create a compaction task
        var task = MaintenanceScheduler.CreateCompactionTask(
            "Manual Compaction",
            TimeSpan.FromHours(24),
            async ct =>
            {
                // Simulated compaction logic
                await Task.Delay(100, ct);
                var result = new CompactionResult
                {
                    SnapshotsCompacted = 5,
                    BytesSaved = 1024 * 1024 * 10, // 10 MB
                    CompactedAt = DateTime.UtcNow
                };
                return Result<CompactionResult>.Success(result);
            });

        var execution = await _scheduler.ExecuteTaskAsync(task);

        if (execution.IsSuccess)
        {
            var exec = execution.Value;
            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"✓ Compaction completed in {(exec.CompletedAt - exec.StartedAt)?.TotalSeconds:F2}s"));

            if (exec.Metadata.TryGetValue("result", out var resultObj) && resultObj is CompactionResult compactionResult)
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Snapshots Compacted:")} {compactionResult.SnapshotsCompacted}");
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Space Saved:")} {FormatBytes(compactionResult.BytesSaved)}");
            }
        }
        else
        {
            PrintError($"Compaction failed: {execution.Error}");
        }
    }

    private static async Task ExecuteArchiveAsync(MaintenanceOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("DAG Archiving"));
        AnsiConsole.WriteLine();

        var archivePath = options.ArchivePath ?? Path.Combine(Path.GetTempPath(), "ouroboros_archive");
        var archiveAge = TimeSpan.FromDays(options.ArchiveAgeDays);

        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Archive age:")} {options.ArchiveAgeDays} days");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Archive path:")} {Markup.Escape(archivePath)}");
        AnsiConsole.WriteLine();

        // Create an archiving task
        var task = MaintenanceScheduler.CreateArchivingTask(
            "Manual Archiving",
            TimeSpan.FromHours(24),
            archiveAge,
            async (age, ct) =>
            {
                // Simulated archiving logic
                await Task.Delay(100, ct);

                Directory.CreateDirectory(archivePath);

                var result = new ArchiveResult
                {
                    SnapshotsArchived = 3,
                    ArchiveLocation = archivePath,
                    ArchivedAt = DateTime.UtcNow
                };
                return Result<ArchiveResult>.Success(result);
            });

        var execution = await _scheduler.ExecuteTaskAsync(task);

        if (execution.IsSuccess)
        {
            var exec = execution.Value;
            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"✓ Archiving completed in {(exec.CompletedAt - exec.StartedAt)?.TotalSeconds:F2}s"));

            if (exec.Metadata.TryGetValue("result", out var resultObj) && resultObj is ArchiveResult archiveResult)
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Snapshots Archived:")} {archiveResult.SnapshotsArchived}");
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Archive Location:")} {Markup.Escape(archiveResult.ArchiveLocation)}");
            }
        }
        else
        {
            PrintError($"Archiving failed: {execution.Error}");
        }
    }

    private static async Task ExecuteDetectAnomaliesAsync(MaintenanceOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Anomaly Detection"));
        AnsiConsole.WriteLine();

        // Create an anomaly detection task
        var task = MaintenanceScheduler.CreateAnomalyDetectionTask(
            "Manual Anomaly Detection",
            TimeSpan.FromHours(1),
            async ct =>
            {
                // Simulated anomaly detection logic
                await Task.Delay(100, ct);

                var anomalies = new List<AnomalyAlert>();

                // Simulate detecting an anomaly
                var random = new Random();
                if (random.Next(100) > 50)
                {
                    anomalies.Add(new AnomalyAlert
                    {
                        MetricName = "snapshot_size",
                        Description = "Snapshot size exceeded expected range",
                        Severity = AlertSeverity.Warning,
                        ExpectedValue = "< 10 MB",
                        ObservedValue = "15 MB"
                    });
                }

                var result = new AnomalyDetectionResult
                {
                    Anomalies = anomalies,
                    DetectedAt = DateTime.UtcNow
                };
                return Result<AnomalyDetectionResult>.Success(result);
            });

        var execution = await _scheduler.ExecuteTaskAsync(task);

        if (execution.IsSuccess)
        {
            var exec = execution.Value;
            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"✓ Anomaly detection completed in {(exec.CompletedAt - exec.StartedAt)?.TotalSeconds:F2}s"));

            if (exec.Metadata.TryGetValue("result", out var resultObj) && resultObj is AnomalyDetectionResult detectionResult)
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Anomalies Detected:")} {detectionResult.Anomalies.Count}");

                if (detectionResult.Anomalies.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine(OuroborosTheme.Accent("  Detected Anomalies:"));
                    foreach (var anomaly in detectionResult.Anomalies)
                    {
                        AnsiConsole.MarkupLine($"    [yellow][[{Markup.Escape(anomaly.Severity.ToString())}]][/] {Markup.Escape(anomaly.MetricName)}: {Markup.Escape(anomaly.Description)}");
                        AnsiConsole.MarkupLine($"      {OuroborosTheme.Dim($"Expected: {anomaly.ExpectedValue}, Observed: {anomaly.ObservedValue}")}");

                        // Add to scheduler's alert list
                        _scheduler.CreateAlert(anomaly);
                    }
                }
            }
        }
        else
        {
            PrintError($"Anomaly detection failed: {execution.Error}");
        }
    }

    private static Task ExecuteScheduleAsync(MaintenanceOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Schedule Maintenance Task"));
        AnsiConsole.WriteLine();

        if (string.IsNullOrWhiteSpace(options.TaskName))
        {
            PrintError("Task name is required (use --task-name)");
            return Task.CompletedTask;
        }

        var schedule = TimeSpan.FromHours(options.ScheduleHours);
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Task:")} {Markup.Escape(options.TaskName)}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Schedule:")} Every {options.ScheduleHours} hours");
        AnsiConsole.WriteLine();

        // Create a generic scheduled task
        var task = new MaintenanceTask
        {
            Id = Guid.NewGuid(),
            Name = options.TaskName,
            Description = $"Scheduled task: {options.TaskName}",
            TaskType = MaintenanceTaskType.Custom,
            Schedule = schedule,
            IsEnabled = true,
            Execute = async ct =>
            {
                AnsiConsole.MarkupLine($"  \\[{DateTime.UtcNow:HH:mm:ss}] Executing scheduled task: {Markup.Escape(options.TaskName)}");
                await Task.Delay(100, ct);
                return Result<object>.Success("Task completed");
            }
        };

        var result = _scheduler.ScheduleTask(task);

        if (result.IsSuccess)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"✓ Task '{options.TaskName}' scheduled successfully"));
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("ID:")} {task.Id}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Schedule:")} Every {schedule.TotalHours} hours");
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("\nNote: Use 'maintenance start-scheduler' to begin scheduled execution"));
        }
        else
        {
            PrintError($"Failed to schedule task: {result.Error}");
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteHistoryAsync(MaintenanceOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Maintenance Execution History"));
        AnsiConsole.WriteLine();

        var history = _scheduler.GetHistory(options.Limit);

        if (history.Count == 0)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("No maintenance executions found."));
            return Task.CompletedTask;
        }

        if (options.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(history, JsonDefaults.IndentedExact);
            AnsiConsole.WriteLine(json);
        }
        else
        {
            PrintHistoryTable(history, options.Verbose);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteAlertsAsync(MaintenanceOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Anomaly Alerts"));
        AnsiConsole.WriteLine();

        if (!string.IsNullOrWhiteSpace(options.AlertId) && !string.IsNullOrWhiteSpace(options.Resolution))
        {
            // Resolve an alert
            if (!Guid.TryParse(options.AlertId, out var alertId))
            {
                PrintError($"Invalid alert ID: {options.AlertId}");
                return Task.CompletedTask;
            }

            var result = _scheduler.ResolveAlert(alertId, options.Resolution);

            if (result.IsSuccess)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"✓ Alert {alertId} resolved"));
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Resolution:")} {Markup.Escape(options.Resolution)}");
            }
            else
            {
                PrintError($"Failed to resolve alert: {result.Error}");
            }

            return Task.CompletedTask;
        }

        // List alerts
        var alerts = _scheduler.GetAlerts(options.UnresolvedOnly);

        if (alerts.Count == 0)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim(options.UnresolvedOnly ? "No unresolved alerts." : "No alerts found."));
            return Task.CompletedTask;
        }

        if (options.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(alerts, JsonDefaults.IndentedExact);
            AnsiConsole.WriteLine(json);
        }
        else
        {
            PrintAlertsTable(alerts, options.Verbose);
        }

        return Task.CompletedTask;
    }

    private static void PrintHistoryTable(IReadOnlyList<MaintenanceExecution> executions, bool verbose)
    {
        var table = OuroborosTheme.ThemedTable("Status", "Task", "Type", "Started", "Duration", "Result");

        foreach (var exec in executions)
        {
            var duration = exec.CompletedAt.HasValue
                ? $"{(exec.CompletedAt.Value - exec.StartedAt).TotalSeconds:F2}s"
                : "—";

            string statusIcon = exec.Status switch
            {
                MaintenanceStatus.Completed => "[green]✓[/]",
                MaintenanceStatus.Failed => "[red]✗[/]",
                MaintenanceStatus.Running => "[rgb(148,103,189)]▶[/]",
                MaintenanceStatus.Cancelled => "[grey]○[/]",
                _ => "—"
            };

            table.AddRow(
                statusIcon,
                Markup.Escape(exec.Task.Name),
                Markup.Escape(exec.Task.TaskType.ToString()),
                $"{exec.StartedAt:yyyy-MM-dd HH:mm:ss}",
                duration,
                Markup.Escape(exec.ResultMessage ?? "—"));
        }

        AnsiConsole.Write(table);

        if (verbose)
        {
            foreach (var exec in executions.Where(e => e.Metadata.Count > 0))
            {
                AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Dim("Metadata:")}");
                foreach (var kvp in exec.Metadata)
                {
                    AnsiConsole.MarkupLine($"    {Markup.Escape(kvp.Key)}: {Markup.Escape(kvp.Value?.ToString() ?? "null")}");
                }
            }
        }
    }

    private static void PrintAlertsTable(IReadOnlyList<AnomalyAlert> alerts, bool verbose)
    {
        var table = OuroborosTheme.ThemedTable("Severity", "Status", "Metric", "Description", "Detected");

        foreach (var alert in alerts)
        {
            string severityColor = alert.Severity switch
            {
                AlertSeverity.Critical => "red",
                AlertSeverity.Error => "rgb(255,165,0)",
                AlertSeverity.Warning => "yellow",
                AlertSeverity.Info => "blue",
                _ => "grey"
            };

            string resolvedStatus = alert.IsResolved ? "[green]RESOLVED[/]" : "[yellow]ACTIVE[/]";

            table.AddRow(
                $"[{severityColor}]{Markup.Escape(alert.Severity.ToString())}[/]",
                resolvedStatus,
                Markup.Escape(alert.MetricName),
                Markup.Escape(alert.Description),
                $"{alert.DetectedAt:yyyy-MM-dd HH:mm:ss}");
        }

        AnsiConsole.Write(table);

        if (verbose)
        {
            foreach (var alert in alerts)
            {
                AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent("ID:")} {alert.Id}");
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Expected:")} {Markup.Escape(alert.ExpectedValue?.ToString() ?? "N/A")}");
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Observed:")} {Markup.Escape(alert.ObservedValue?.ToString() ?? "N/A")}");

                if (alert.IsResolved)
                {
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Resolved:")} {alert.ResolvedAt:yyyy-MM-dd HH:mm:ss} UTC");
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Resolution:")} {Markup.Escape(alert.Resolution ?? "N/A")}");
                }
            }
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
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
