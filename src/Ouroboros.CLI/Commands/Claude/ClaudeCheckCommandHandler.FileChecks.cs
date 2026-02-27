// <copyright file="ClaudeCheckCommandHandler.FileChecks.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Spectre.Console;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Partial class for file-based diagnostic checks: CLAUDE.md consistency and memory files.
/// </summary>
public sealed partial class ClaudeCheckCommandHandler
{
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
}
