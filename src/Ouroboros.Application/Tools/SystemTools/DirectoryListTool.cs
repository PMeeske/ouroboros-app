// <copyright file="DirectoryListTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;

/// <summary>
/// List directory contents.
/// </summary>
internal class DirectoryListTool : ITool
{
    public string Name => "list_directory";
    public string Description => "List files and folders in a directory. Input: path (supports %ENV% vars)";
    public string? JsonSchema => null;

    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var raw = input.Trim().Trim('"');
            var path = string.IsNullOrEmpty(raw)
                ? Environment.CurrentDirectory
                : PathSanitizer.Sanitize(raw);

            if (!Directory.Exists(path))
                return Task.FromResult(Result<string, string>.Failure($"Directory not found: {path}"));

            var sb = new StringBuilder();
            sb.AppendLine($"[DIR] {path}");
            sb.AppendLine();

            var dirs = Directory.GetDirectories(path).Take(50);
            foreach (var dir in dirs)
            {
                var name = Path.GetFileName(dir);
                sb.AppendLine($"  [DIR]  {name}/");
            }

            var files = Directory.GetFiles(path).Take(100);
            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                sb.AppendLine($"  [FILE] {fi.Name} ({ByteFormatter.Format(fi.Length)})");
            }

            var totalDirs = Directory.GetDirectories(path).Length;
            var totalFiles = Directory.GetFiles(path).Length;
            sb.AppendLine();
            sb.AppendLine($"Total: {totalDirs} folders, {totalFiles} files");

            return Task.FromResult(Result<string, string>.Success(sb.ToString()));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result<string, string>.Failure(ex.Message));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure(ex.Message));
        }
    }
}
