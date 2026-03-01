// <copyright file="ClaudeCheckCommandHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Ouroboros.ApiHost.Services;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Core.Configuration;
using Ouroboros.Domain.Vectors;
using Qdrant.Client;
using Spectre.Console;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Diagnostic command handler that checks Qdrant health (local + cloud),
/// Claude Code memory files, CLAUDE.md consistency, and submodule sync state.
/// </summary>
public sealed partial class ClaudeCheckCommandHandler
{
    private readonly ISpectreConsoleService _console;
    private readonly QdrantClient _qdrantClient;
    private readonly IQdrantCollectionRegistry _registry;
    private readonly QdrantSettings _qdrantSettings;
    private readonly IQdrantSyncService _syncService;
    private readonly ILogger<ClaudeCheckCommandHandler> _logger;

    public ClaudeCheckCommandHandler(
        ISpectreConsoleService console,
        QdrantClient qdrantClient,
        IQdrantCollectionRegistry registry,
        QdrantSettings qdrantSettings,
        IQdrantSyncService syncService,
        ILogger<ClaudeCheckCommandHandler> logger)
    {
        _console = console;
        _qdrantClient = qdrantClient;
        _registry = registry;
        _qdrantSettings = qdrantSettings;
        _syncService = syncService;
        _logger = logger;
        _ = _registry; // S4487: DI-injected, retained for lifetime
    }

    public async Task<int> HandleAsync(CancellationToken ct = default)
    {
        _console.Write(new FigletText("Claude Check").Color(Color.Cyan1));
        _console.MarkupLine("[dim]Ouroboros memory & sync diagnostics[/]\n");

        int issues = 0;
        issues += await CheckQdrantLocalAsync(ct);
        issues += await CheckQdrantCloudAsync(ct);
        issues += CheckClaudeFiles();
        issues += CheckMemoryFiles();
        issues += await CheckSubmoduleSyncAsync(ct);
        issues += CheckMemoryLimits();

        _console.WriteLine();
        if (issues == 0)
        {
            _console.MarkupLine("[green bold]All checks passed.[/]");
        }
        else
        {
            _console.MarkupLine($"[yellow bold]{issues} issue(s) detected.[/]");
        }

        return issues > 0 ? 1 : 0;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 1: Qdrant Local Health
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<int> CheckQdrantLocalAsync(CancellationToken ct)
    {
        _console.Write(new Spectre.Console.Rule("[cyan bold]Qdrant Local Health[/]").RuleStyle("cyan dim"));
        _console.WriteLine();

        bool reachable = await PingHttpAsync($"{_qdrantSettings.HttpEndpoint}/healthz");
        if (!reachable)
        {
            _console.MarkupLine($"[red]UNREACHABLE[/] at {Markup.Escape(_qdrantSettings.HttpEndpoint)}");
            _console.MarkupLine("[dim]Start Qdrant or check Ouroboros:Qdrant settings.[/]\n");
            return 1;
        }

        try
        {
            var collections = await _qdrantClient.ListCollectionsAsync(ct);
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Cyan1)
                .AddColumn("[bold]Collection[/]")
                .AddColumn("[bold]Status[/]")
                .AddColumn("[bold]Dim[/]", c => c.RightAligned())
                .AddColumn("[bold]Points[/]", c => c.RightAligned())
                .AddColumn("[bold]Issues[/]");

            int issues = 0;

            foreach (var name in collections.OrderBy(c => c))
            {
                try
                {
                    var info = await _qdrantClient.GetCollectionInfoAsync(name, ct);
                    var vectorParams = info.Config.Params.VectorsConfig?.Params;
                    int dim = vectorParams is not null ? (int)vectorParams.Size : 0;
                    long points = (long)info.PointsCount;
                    var status = info.Status.ToString();

                    var issueList = new List<string>();
                    if (dim != _qdrantSettings.DefaultVectorSize && dim > 0)
                    {
                        issueList.Add($"[yellow]dim {dim} != {_qdrantSettings.DefaultVectorSize}[/]");
                    }

                    if (points == 0)
                    {
                        issueList.Add("[dim]empty[/]");
                    }

                    string statusMarkup = status.Contains("Green", StringComparison.OrdinalIgnoreCase)
                        ? "[green]OK[/]"
                        : $"[yellow]{Markup.Escape(status)}[/]";

                    issues += issueList.Count(i => i.Contains("yellow"));

                    table.AddRow(
                        Markup.Escape(name),
                        statusMarkup,
                        dim.ToString(),
                        points.ToString("N0"),
                        issueList.Count > 0 ? string.Join(", ", issueList) : "[green]none[/]");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Failed to get info for collection {Name}", name);
                    table.AddRow(Markup.Escape(name), "[red]ERROR[/]", "-", "-", Markup.Escape(ex.Message));
                    issues++;
                }
                catch (System.Net.Http.HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Failed to get info for collection {Name}", name);
                    table.AddRow(Markup.Escape(name), "[red]ERROR[/]", "-", "-", Markup.Escape(ex.Message));
                    issues++;
                }
            }

            _console.Write(table);
            _console.MarkupLine($"[dim]{collections.Count} collection(s) on local Qdrant.[/]\n");
            return issues;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to query local Qdrant");
            _console.MarkupLine($"[red]Failed to query Qdrant:[/] {Markup.Escape(ex.Message)}\n");
            return 1;
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to query local Qdrant");
            _console.MarkupLine($"[red]Failed to query Qdrant:[/] {Markup.Escape(ex.Message)}\n");
            return 1;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 2: Qdrant Cloud Sync
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<int> CheckQdrantCloudAsync(CancellationToken ct)
    {
        _console.Write(new Spectre.Console.Rule("[cyan bold]Qdrant Cloud Sync[/]").RuleStyle("cyan dim"));
        _console.WriteLine();

        try
        {
            var status = await _syncService.GetStatusAsync(ct);

            // Status overview
            var statusTable = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("[bold]Endpoint[/]")
                .AddColumn("[bold]Status[/]")
                .AddColumn("[bold]Collections[/]");

            statusTable.AddRow(
                Markup.Escape(status.Local.Endpoint),
                status.Local.Online ? "[green]ONLINE[/]" : "[red]OFFLINE[/]",
                status.Local.CollectionCount.ToString());

            statusTable.AddRow(
                status.Cloud.Endpoint.Length > 0 ? Markup.Escape(status.Cloud.Endpoint) : "[dim]not configured[/]",
                status.Cloud.Online ? "[green]ONLINE[/]" : "[red]OFFLINE[/]",
                status.Cloud.CollectionCount.ToString());

            _console.Write(statusTable);

            if (status.EncryptionActive)
            {
                _console.MarkupLine($"[dim]Encryption: {Markup.Escape(status.EncryptionCurve ?? "active")}[/]");
            }

            if (!status.Ready)
            {
                _console.MarkupLine("[yellow]Cloud sync not ready[/] — check Qdrant:Cloud settings.\n");
                return 1;
            }

            // Diff
            var diff = await _syncService.GetDiffAsync(ct);

            var diffTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Cyan1)
                .AddColumn("[bold]Collection[/]")
                .AddColumn("[bold]Local Pts[/]", c => c.RightAligned())
                .AddColumn("[bold]Cloud Pts[/]", c => c.RightAligned())
                .AddColumn("[bold]Status[/]");

            foreach (var col in diff.Collections)
            {
                string statusMarkup = col.Status switch
                {
                    "synced" => "[green]SYNCED[/]",
                    "diverged" => "[red]DIVERGED[/]",
                    "local_only" => "[yellow]LOCAL ONLY[/]",
                    "cloud_only" => "[yellow]CLOUD ONLY[/]",
                    _ => Markup.Escape(col.Status),
                };

                diffTable.AddRow(
                    Markup.Escape(col.Name),
                    col.LocalPoints?.ToString("N0") ?? "-",
                    col.CloudPoints?.ToString("N0") ?? "-",
                    statusMarkup);
            }

            _console.Write(diffTable);
            _console.MarkupLine(
                $"[dim]Synced: {diff.Synced}  Diverged: {diff.Diverged}  " +
                $"Local-only: {diff.LocalOnly}  Cloud-only: {diff.CloudOnly}[/]\n");

            return diff.Diverged + diff.LocalOnly;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
        {
            _console.MarkupLine("[yellow]Cloud sync not configured.[/]");
            _console.MarkupLine("[dim]Set Ouroboros:Qdrant:Cloud:Endpoint, ApiKey, and Enabled=true.[/]\n");
            return 0; // Not an error if intentionally unconfigured
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to check cloud sync");
            _console.MarkupLine($"[red]Cloud check failed:[/] {Markup.Escape(ex.Message)}\n");
            return 1;
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to check cloud sync");
            _console.MarkupLine($"[red]Cloud check failed:[/] {Markup.Escape(ex.Message)}\n");
            return 1;
        }
    }
}
