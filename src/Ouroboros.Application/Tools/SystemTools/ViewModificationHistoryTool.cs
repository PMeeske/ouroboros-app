// <copyright file="ViewModificationHistoryTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;

/// <summary>
/// View my own modification history (backups).
/// </summary>
internal class ViewModificationHistoryTool : ITool
{
    public string Name => "view_modification_history";
    public string Description => "View history of self-modifications I've made. Lists all backup files created when I modified my own code.";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        try
        {
            var srcDir = Path.Combine(Environment.CurrentDirectory, "src");
            if (!Directory.Exists(srcDir))
            {
                return Result<string, string>.Failure("Source directory not found.");
            }

            var backupFiles = Directory.GetFiles(srcDir, "*.backup.*", SearchOption.AllDirectories)
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .Take(20)
                .ToList();

            if (backupFiles.Count == 0)
            {
                return Result<string, string>.Success("No self-modification history found. I haven't modified my code yet.");
            }

            var sb = new StringBuilder();
            sb.AppendLine("**Self-Modification History**\n");

            foreach (var backup in backupFiles)
            {
                var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, backup);
                var modified = File.GetLastWriteTime(backup);
                sb.AppendLine($"- `{relativePath}` - {modified:yyyy-MM-dd HH:mm:ss}");
            }

            sb.AppendLine("\n_These are backups of files before I modified them._");

            return Result<string, string>.Success(sb.ToString());
        }
        catch (IOException ex)
        {
            return Result<string, string>.Failure($"Failed to retrieve history: {ex.Message}");
        }
    }
}
