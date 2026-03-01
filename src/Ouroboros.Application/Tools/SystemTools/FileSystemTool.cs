// <copyright file="FileSystemTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text.Json;

/// <summary>
/// File system navigation and info tool.
/// </summary>
internal class FileSystemTool : ITool
{
    public string Name => "file_system";
    public string Description => "Navigate and get info about files/folders. Input: JSON {\"action\":\"exists|info|size|modified\", \"path\":\"...\"}";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(input);
            var action = args.GetProperty("action").GetString() ?? "exists";
            var rawPath = args.GetProperty("path").GetString() ?? "";

            var path = PathSanitizer.Sanitize(rawPath);

            return action.ToLower() switch
            {
                "exists" => Result<string, string>.Success(
                    (File.Exists(path) || Directory.Exists(path)).ToString()),
                "info" => GetFileInfo(path),
                "size" => GetSize(path),
                "modified" => GetModified(path),
                _ => Result<string, string>.Failure($"Unknown action: {action}")
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<string, string>.Failure(ex.Message);
        }
        catch (IOException ex)
        {
            return Result<string, string>.Failure(ex.Message);
        }
    }

    private static Result<string, string> GetFileInfo(string path)
    {
        if (File.Exists(path))
        {
            var fi = new FileInfo(path);
            return Result<string, string>.Success(JsonSerializer.Serialize(new
            {
                type = "file",
                name = fi.Name,
                fullPath = fi.FullName,
                size = fi.Length,
                created = fi.CreationTime,
                modified = fi.LastWriteTime,
                extension = fi.Extension,
                readOnly = fi.IsReadOnly
            }));
        }
        else if (Directory.Exists(path))
        {
            var di = new DirectoryInfo(path);
            return Result<string, string>.Success(JsonSerializer.Serialize(new
            {
                type = "directory",
                name = di.Name,
                fullPath = di.FullName,
                created = di.CreationTime,
                modified = di.LastWriteTime,
                fileCount = di.GetFiles().Length,
                dirCount = di.GetDirectories().Length
            }));
        }
        return Result<string, string>.Failure("Path not found");
    }

    private static Result<string, string> GetSize(string path)
    {
        if (File.Exists(path))
            return Result<string, string>.Success(new FileInfo(path).Length.ToString());
        if (Directory.Exists(path))
        {
            var size = new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            return Result<string, string>.Success(size.ToString());
        }
        return Result<string, string>.Failure("Path not found");
    }

    private static Result<string, string> GetModified(string path)
    {
        if (File.Exists(path))
            return Result<string, string>.Success(File.GetLastWriteTime(path).ToString("o"));
        if (Directory.Exists(path))
            return Result<string, string>.Success(Directory.GetLastWriteTime(path).ToString("o"));
        return Result<string, string>.Failure("Path not found");
    }
}
