// <copyright file="ClaudeRestoreCommand.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text.Json;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Restores CLAUDE.md and MEMORY.md files from a backup created by <see cref="ClaudeBackupCommand"/>.
/// </summary>
public static class ClaudeRestoreCommand
{
    public static async Task<int> RunAsync(IAnsiConsole console, string backupPath)
    {
        console.MarkupLine("[bold]Claude Restore[/]\n");

        var manifestPath = Path.Combine(backupPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            console.MarkupLine($"[red]manifest.json not found in {Markup.Escape(backupPath)}[/]");
            return 1;
        }

        var json = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        if (manifest is null)
        {
            console.MarkupLine("[red]Failed to parse manifest.json.[/]");
            return 1;
        }

        console.MarkupLine($"[dim]Backup from: {manifest.Timestamp:yyyy-MM-dd HH:mm:ss} UTC[/]");
        console.MarkupLine($"[dim]Meta-repo: {Markup.Escape(manifest.MetaRepoRoot)}[/]");
        console.MarkupLine($"[dim]Files: {manifest.Files.Count}[/]\n");

        // Verify backup integrity
        int corruptCount = 0;
        foreach (var entry in manifest.Files)
        {
            string sourcePath;
            if (entry.RelativePath.StartsWith("~/.claude/projects/"))
            {
                var relFromProjects = entry.RelativePath["~/.claude/projects/".Length..];
                sourcePath = Path.Combine(backupPath, "memory-files", relFromProjects);
            }
            else
            {
                sourcePath = Path.Combine(backupPath, "claude-files", entry.RelativePath);
            }

            if (!File.Exists(sourcePath))
            {
                console.MarkupLine($"[yellow]Missing in backup:[/] {Markup.Escape(entry.RelativePath)}");
                corruptCount++;
                continue;
            }

            var bytes = File.ReadAllBytes(sourcePath);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (hash != entry.Sha256)
            {
                console.MarkupLine($"[red]Hash mismatch:[/] {Markup.Escape(entry.RelativePath)}");
                corruptCount++;
            }
        }

        if (corruptCount > 0)
        {
            console.MarkupLine($"\n[red]{corruptCount} file(s) have integrity issues. Aborting.[/]");
            return 1;
        }

        console.MarkupLine("[green]Backup integrity verified.[/]\n");

        // Confirm
        if (!console.Confirm("Restore files? This will overwrite existing files."))
        {
            console.MarkupLine("[dim]Cancelled.[/]");
            return 0;
        }

        // Restore
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var restorePaths = manifest.Files.Select(entry =>
        {
            if (entry.RelativePath.StartsWith("~/.claude/projects/"))
            {
                var relFromProjects = entry.RelativePath["~/.claude/projects/".Length..];
                return (
                    Source: Path.Combine(backupPath, "memory-files", relFromProjects),
                    Dest: Path.Combine(home, ".claude", "projects", relFromProjects));
            }
            return (
                Source: Path.Combine(backupPath, "claude-files", entry.RelativePath),
                Dest: Path.Combine(manifest.MetaRepoRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        });

        int restored = 0;
        foreach (var (sourcePath, destPath) in restorePaths)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(sourcePath, destPath, overwrite: true);
            restored++;
        }

        console.MarkupLine($"\n[green]{restored} file(s) restored.[/]");
        return 0;
    }

    private sealed class BackupManifest
    {
        public DateTime Timestamp { get; set; }
        public string MetaRepoRoot { get; set; } = "";
        public List<BackupFileEntry> Files { get; set; } = [];
        public Dictionary<string, string> SubmoduleCommits { get; set; } = new();
    }

    private sealed class BackupFileEntry
    {
        public string RelativePath { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public int SizeBytes { get; set; }
        public DateTime LastModified { get; set; }
    }
}
