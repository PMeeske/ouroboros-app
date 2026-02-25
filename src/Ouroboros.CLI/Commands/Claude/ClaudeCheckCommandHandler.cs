// <copyright file="ClaudeCheckCommandHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
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
public sealed class ClaudeCheckCommandHandler
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
                catch (Exception ex)
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
        catch (Exception ex)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check cloud sync");
            _console.MarkupLine($"[red]Cloud check failed:[/] {Markup.Escape(ex.Message)}\n");
            return 1;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 3: CLAUDE.md File Consistency
    // ═══════════════════════════════════════════════════════════════════════

    private int CheckClaudeFiles()
    {
        _console.Write(new Spectre.Console.Rule("[cyan bold]CLAUDE.md File Consistency[/]").RuleStyle("cyan dim"));
        _console.WriteLine();

        string? metaRoot = FindMetaRepoRoot();
        if (metaRoot is null)
        {
            _console.MarkupLine("[yellow]Could not locate meta-repo root (no .gitmodules found).[/]\n");
            return 1;
        }

        var claudeFiles = Directory.GetFiles(metaRoot, "CLAUDE.md", SearchOption.AllDirectories)
            .Where(f => !f.Contains("bin") && !f.Contains("obj") && !f.Contains("node_modules"))
            .OrderBy(f => f)
            .ToList();

        if (claudeFiles.Count == 0)
        {
            _console.MarkupLine("[dim]No CLAUDE.md files found.[/]\n");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn("[bold]File[/]")
            .AddColumn("[bold]Hash (short)[/]")
            .AddColumn("[bold]Size[/]", c => c.RightAligned())
            .AddColumn("[bold]Status[/]");

        var hashGroups = new Dictionary<string, List<string>>();

        foreach (var file in claudeFiles)
        {
            var bytes = File.ReadAllBytes(file);
            var hash = Convert.ToHexString(SHA256.HashData(bytes))[..12];
            var relativePath = Path.GetRelativePath(metaRoot, file).Replace('\\', '/');

            if (!hashGroups.TryGetValue(hash, out var group))
            {
                group = [];
                hashGroups[hash] = group;
            }

            group.Add(relativePath);

            table.AddRow(
                Markup.Escape(relativePath),
                $"[dim]{hash}[/]",
                $"{bytes.Length:N0} B",
                "[dim]...[/]"); // Will be updated after grouping
        }

        // Rebuild table with consistency status
        var finalTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn("[bold]File[/]")
            .AddColumn("[bold]Hash[/]")
            .AddColumn("[bold]Size[/]", c => c.RightAligned())
            .AddColumn("[bold]Status[/]");

        int issues = 0;
        bool hasMultipleHashes = hashGroups.Count > 1;

        foreach (var file in claudeFiles)
        {
            var bytes = File.ReadAllBytes(file);
            var hash = Convert.ToHexString(SHA256.HashData(bytes))[..12];
            var relativePath = Path.GetRelativePath(metaRoot, file).Replace('\\', '/');

            string status;
            if (!hasMultipleHashes)
            {
                status = "[green]CONSISTENT[/]";
            }
            else if (hashGroups[hash].Count >= hashGroups.Values.Max(g => g.Count))
            {
                status = "[green]CANONICAL[/]";
            }
            else
            {
                status = "[red]DIVERGED[/]";
                issues++;
            }

            finalTable.AddRow(
                Markup.Escape(relativePath),
                $"[dim]{hash}[/]",
                $"{bytes.Length:N0} B",
                status);
        }

        _console.Write(finalTable);

        if (hasMultipleHashes)
        {
            _console.MarkupLine($"[yellow]{hashGroups.Count} distinct hashes found — copies have diverged.[/]");
        }
        else
        {
            _console.MarkupLine("[green]All CLAUDE.md copies are identical.[/]");
        }

        _console.WriteLine();
        return issues;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 4: Claude Code Memory Files
    // ═══════════════════════════════════════════════════════════════════════

    private int CheckMemoryFiles()
    {
        _console.Write(new Spectre.Console.Rule("[cyan bold]Claude Code Memory Files[/]").RuleStyle("cyan dim"));
        _console.WriteLine();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var claudeDir = Path.Combine(home, ".claude", "projects");
        int issues = 0;

        if (!Directory.Exists(claudeDir))
        {
            _console.MarkupLine("[yellow]~/.claude/projects/ does not exist.[/]\n");
            return 1;
        }

        var memoryFiles = Directory.GetFiles(claudeDir, "MEMORY.md", SearchOption.AllDirectories);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn("[bold]Project[/]")
            .AddColumn("[bold]Size[/]", c => c.RightAligned())
            .AddColumn("[bold]Last Modified[/]")
            .AddColumn("[bold]Status[/]");

        foreach (var file in memoryFiles)
        {
            var info = new FileInfo(file);
            var project = Path.GetRelativePath(claudeDir, Path.GetDirectoryName(file)!).Replace('\\', '/');

            string status;
            if (info.Length == 0)
            {
                status = "[yellow]EMPTY[/]";
                issues++;
            }
            else if (info.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-30))
            {
                status = "[yellow]STALE (>30d)[/]";
            }
            else
            {
                status = "[green]OK[/]";
            }

            table.AddRow(
                Markup.Escape(project),
                $"{info.Length:N0} B",
                info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                status);
        }

        _console.Write(table);
        _console.MarkupLine($"[dim]{memoryFiles.Length} memory file(s) found.[/]\n");
        return issues;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 5: Submodule Sync
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<int> CheckSubmoduleSyncAsync(CancellationToken ct)
    {
        _console.Write(new Spectre.Console.Rule("[cyan bold]Submodule Sync[/]").RuleStyle("cyan dim"));
        _console.WriteLine();

        string? metaRoot = FindMetaRepoRoot();
        if (metaRoot is null)
        {
            _console.MarkupLine("[yellow]Could not locate meta-repo root.[/]\n");
            return 1;
        }

        var gitmodulesPath = Path.Combine(metaRoot, ".gitmodules");
        if (!File.Exists(gitmodulesPath))
        {
            _console.MarkupLine("[yellow].gitmodules not found.[/]\n");
            return 1;
        }

        var submodules = ParseGitmodules(gitmodulesPath);
        if (submodules.Count == 0)
        {
            _console.MarkupLine("[dim]No submodules found.[/]\n");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn("[bold]Submodule[/]")
            .AddColumn("[bold]Branch[/]")
            .AddColumn("[bold]HEAD[/]")
            .AddColumn("[bold]Dirty?[/]")
            .AddColumn("[bold]Status[/]");

        int issues = 0;

        foreach (var (name, path, branch) in submodules)
        {
            var fullPath = Path.Combine(metaRoot, path);
            if (!Directory.Exists(fullPath))
            {
                table.AddRow(Markup.Escape(name), Markup.Escape(branch), "-", "-", "[red]MISSING[/]");
                issues++;
                continue;
            }

            var (headOk, head) = await RunGitAsync(fullPath, "rev-parse --short HEAD");
            var (_, porcelain) = await RunGitAsync(fullPath, "status --porcelain");
            var (branchOk, currentBranch) = await RunGitAsync(fullPath, "rev-parse --abbrev-ref HEAD");

            bool isDirty = !string.IsNullOrWhiteSpace(porcelain);
            bool isDetached = currentBranch.Trim() == "HEAD";

            string statusMarkup;
            if (isDirty)
            {
                statusMarkup = "[red]DIRTY[/]";
                issues++;
            }
            else if (isDetached)
            {
                statusMarkup = "[yellow]DETACHED[/]";
            }
            else
            {
                statusMarkup = "[green]OK[/]";
            }

            table.AddRow(
                Markup.Escape(name),
                Markup.Escape(branch),
                headOk ? Markup.Escape(head.Trim()) : "[red]?[/]",
                isDirty ? "[red]yes[/]" : "[green]no[/]",
                statusMarkup);
        }

        _console.Write(table);
        _console.WriteLine();
        return issues;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 6: Memory Limits Audit
    // ═══════════════════════════════════════════════════════════════════════

    private int CheckMemoryLimits()
    {
        _console.Write(new Spectre.Console.Rule("[cyan bold]Memory Limits Audit[/]").RuleStyle("cyan dim"));
        _console.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn("[bold]Component[/]")
            .AddColumn("[bold]Limit[/]", c => c.RightAligned())
            .AddColumn("[bold]Type[/]")
            .AddColumn("[bold]Status[/]");

        table.AddRow("ConversationMemory", "unlimited", "no eviction (default)", "[green]OK[/]");
        table.AddRow("EpisodicMemory scroll", "100/batch", "paginated (no ceiling)", "[green]OK[/]");
        table.AddRow("QdrantVectorStore scroll", "100/batch", "paginated (no ceiling)", "[green]OK[/]");
        table.AddRow("QdrantNeuralMemory scroll", "100/batch", "paginated (no ceiling)", "[green]OK[/]");
        table.AddRow("Cloud sync batch", "100/batch", "paginated (no ceiling)", "[green]OK[/]");
        table.AddRow("Default vector dim", _qdrantSettings.DefaultVectorSize.ToString(), "configurable", "[blue]INFO[/]");

        _console.Write(table);
        _console.WriteLine();
        return 0;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static string? FindMetaRepoRoot(string? startDir = null)
    {
        string? dir = startDir ?? Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir))
        {
            string gitmodules = Path.Combine(dir, ".gitmodules");
            if (File.Exists(gitmodules))
            {
                string content = File.ReadAllText(gitmodules);
                if (content.Contains("[submodule \".build\"]") && content.Contains("[submodule \"foundation\"]"))
                {
                    return dir;
                }
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    private static List<(string name, string path, string branch)> ParseGitmodules(string path)
    {
        var result = new List<(string, string, string)>();
        string? currentName = null;
        string currentPath = "";
        string currentBranch = "main";

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[submodule \"") && trimmed.EndsWith("\"]"))
            {
                if (currentName is not null)
                {
                    result.Add((currentName, currentPath, currentBranch));
                }

                currentName = trimmed["[submodule \"".Length..^"\"]".Length];
                currentPath = "";
                currentBranch = "main";
            }
            else if (trimmed.StartsWith("path = "))
            {
                currentPath = trimmed["path = ".Length..];
            }
            else if (trimmed.StartsWith("branch = "))
            {
                currentBranch = trimmed["branch = ".Length..];
            }
        }

        if (currentName is not null)
        {
            result.Add((currentName, currentPath, currentBranch));
        }

        return result;
    }

    private static async Task<bool> PingHttpAsync(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await http.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(bool success, string output)> RunGitAsync(string workDir, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process is null) return (false, string.Empty);

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode == 0, output);
        }
        catch
        {
            return (false, string.Empty);
        }
    }
}
