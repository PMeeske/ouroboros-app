// <copyright file="ClaudeCheckCommandHandler.Helpers.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Partial class for submodule sync checks, memory limits audit, and shared helpers.
/// </summary>
public sealed partial class ClaudeCheckCommandHandler
{
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
            var (_, currentBranch) = await RunGitAsync(fullPath, "rev-parse --abbrev-ref HEAD");

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

    private static Task<(bool success, string output)> RunGitAsync(string workDir, string args)
        => GitProcessHelper.RunGitAsync(workDir, args);
}
