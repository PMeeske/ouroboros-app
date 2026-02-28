// <copyright file="FileSearchTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;
using System.Text.Json;

/// <summary>
/// Search for files.
/// </summary>
internal class FileSearchTool : ITool
{
    public string Name => "search_files";
    public string Description => "Search for files. Input: JSON {\"path\":\"...\", \"pattern\":\"*.cs\", \"recursive\":true, \"contains\":\"text\"}";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(input);
            var rawPath = args.GetProperty("path").GetString() ?? ".";
            var pattern = args.TryGetProperty("pattern", out var patEl) ? patEl.GetString() ?? "*" : "*";
            var recursive = !args.TryGetProperty("recursive", out var recEl) || recEl.GetBoolean();
            var contains = args.TryGetProperty("contains", out var contEl) ? contEl.GetString() : null;

            var path = PathSanitizer.Sanitize(rawPath);

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.EnumerateFiles(path, pattern, searchOption).Take(200).ToList();

            int skipped = 0;
            if (!string.IsNullOrEmpty(contains))
            {
                var matching = new List<string>();
                foreach (var file in files)
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(file, ct);
                        if (content.Contains(contains, StringComparison.OrdinalIgnoreCase))
                            matching.Add(file);
                    }
                    catch
                    {
                        // File cannot be read (permissions, locked, etc.) - skip it
                        skipped++;
                    }
                }
                files = matching;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {files.Count} files matching '{pattern}':");
            foreach (var file in files.Take(50))
                sb.AppendLine($"  {file}");
            if (files.Count > 50)
                sb.AppendLine($"  ... and {files.Count - 50} more");
            if (skipped > 0)
                sb.AppendLine($"  (Skipped {skipped} inaccessible file(s))");

            return Result<string, string>.Success(sb.ToString());
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
}
