// <copyright file="ClaudeBackupCommand.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text.Json;
using Ouroboros.Application.Json;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Backs up all CLAUDE.md and MEMORY.md files with a manifest for integrity verification.
/// </summary>
public static class ClaudeBackupCommand
{
    public static async Task<int> RunAsync(IAnsiConsole console, string? outputPath = null)
    {
        console.MarkupLine("[bold]Claude Backup[/]\n");

        string? metaRoot = FindMetaRepoRoot();
        if (metaRoot is null)
        {
            console.MarkupLine("[red]Could not locate meta-repo root.[/]");
            return 1;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupDir = outputPath ?? Path.Combine(home, ".claude", "backups", timestamp);
        Directory.CreateDirectory(backupDir);

        var manifest = new BackupManifest
        {
            Timestamp = DateTime.UtcNow,
            MetaRepoRoot = metaRoot,
            Files = [],
            SubmoduleCommits = new Dictionary<string, string>(),
        };

        // Backup CLAUDE.md files
        var claudeFiles = Directory.GetFiles(metaRoot, "CLAUDE.md", SearchOption.AllDirectories)
            .Where(f => !f.Contains("bin") && !f.Contains("obj"))
            .ToList();

        foreach (var file in claudeFiles)
        {
            var relativePath = Path.GetRelativePath(metaRoot, file).Replace('\\', '/');
            var destPath = Path.Combine(backupDir, "claude-files", relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(file, destPath, overwrite: true);

            var bytes = File.ReadAllBytes(file);
            manifest.Files.Add(new BackupFileEntry
            {
                RelativePath = relativePath,
                Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                SizeBytes = bytes.Length,
                LastModified = File.GetLastWriteTimeUtc(file),
            });
        }

        // Backup MEMORY.md files
        var claudeDir = Path.Combine(home, ".claude", "projects");
        if (Directory.Exists(claudeDir))
        {
            foreach (var file in Directory.GetFiles(claudeDir, "MEMORY.md", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(claudeDir, file).Replace('\\', '/');
                var destPath = Path.Combine(backupDir, "memory-files", relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(file, destPath, overwrite: true);

                var bytes = File.ReadAllBytes(file);
                manifest.Files.Add(new BackupFileEntry
                {
                    RelativePath = $"~/.claude/projects/{relativePath}",
                    Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                    SizeBytes = bytes.Length,
                    LastModified = File.GetLastWriteTimeUtc(file),
                });
            }
        }

        // Record submodule HEADs
        var gitmodulesPath = Path.Combine(metaRoot, ".gitmodules");
        if (File.Exists(gitmodulesPath))
        {
            foreach (var line in File.ReadAllLines(gitmodulesPath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("path = "))
                {
                    var subPath = trimmed["path = ".Length..];
                    var fullPath = Path.Combine(metaRoot, subPath);
                    if (Directory.Exists(fullPath))
                    {
                        var (ok, sha) = await RunGitAsync(fullPath, "rev-parse HEAD");
                        if (ok)
                        {
                            manifest.SubmoduleCommits[subPath] = sha.Trim();
                        }
                    }
                }
            }
        }

        // Write manifest
        var manifestPath = Path.Combine(backupDir, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, JsonDefaults.IndentedExact);
        await File.WriteAllTextAsync(manifestPath, json);

        // Summary
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Item")
            .AddColumn("Count");

        table.AddRow("CLAUDE.md files", claudeFiles.Count.ToString());
        table.AddRow("MEMORY.md files", manifest.Files.Count(f => f.RelativePath.Contains("MEMORY")).ToString());
        table.AddRow("Submodule commits", manifest.SubmoduleCommits.Count.ToString());

        console.Write(table);
        console.MarkupLine($"\n[green]Backup saved to:[/] {Markup.Escape(backupDir)}");
        return 0;
    }

    private static string? FindMetaRepoRoot()
    {
        string? dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir))
        {
            string gitmodules = Path.Combine(dir, ".gitmodules");
            if (File.Exists(gitmodules))
            {
                string content = File.ReadAllText(gitmodules);
                if (content.Contains("[submodule \".build\"]") && content.Contains("[submodule \"foundation\"]"))
                    return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    private static Task<(bool success, string output)> RunGitAsync(string workDir, string args)
        => Infrastructure.GitProcessHelper.RunGitAsync(workDir, args);

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
