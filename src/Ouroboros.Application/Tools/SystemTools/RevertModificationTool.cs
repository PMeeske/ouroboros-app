// <copyright file="RevertModificationTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text.RegularExpressions;

/// <summary>
/// Revert a self-modification by restoring from backup.
/// </summary>
internal partial class RevertModificationTool : ITool
{
    public string Name => "revert_modification";
    public string Description => "Revert a self-modification by restoring from a backup file. Input: path to backup file (e.g., 'src/file.cs.backup.20241212_153000').";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var backupPath = input.Trim().Trim('"');
            if (!Path.IsPathRooted(backupPath))
            {
                backupPath = Path.Combine(Environment.CurrentDirectory, backupPath);
            }

            if (!File.Exists(backupPath))
            {
                return Result<string, string>.Failure($"Backup file not found: {backupPath}");
            }

            // Extract original file path by removing .backup.* suffix
            var originalPath = BackupSuffixRegex().Replace(backupPath, "");

            if (originalPath == backupPath)
            {
                return Result<string, string>.Failure("Invalid backup file format. Expected pattern: file.ext.backup.YYYYMMDD_HHMMSS");
            }

            var backupContent = await File.ReadAllTextAsync(backupPath, ct);

            // Create a backup of current state before reverting
            if (File.Exists(originalPath))
            {
                var currentContent = await File.ReadAllTextAsync(originalPath, ct);
                var revertBackup = originalPath + $".pre-revert.{DateTime.Now:yyyyMMdd_HHmmss}";
                await File.WriteAllTextAsync(revertBackup, currentContent, ct);
            }

            await File.WriteAllTextAsync(originalPath, backupContent, ct);

            var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, originalPath);
            return Result<string, string>.Success($"Reverted **{relativePath}** from backup.\n\nRun `dotnet build` to compile the reverted code.");
        }
        catch (IOException ex)
        {
            return Result<string, string>.Failure($"Revert failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<string, string>.Failure($"Revert failed: {ex.Message}");
        }
    }

    [GeneratedRegex(@"\.backup\.\d{8}_\d{6}$")]
    private static partial Regex BackupSuffixRegex();
}
