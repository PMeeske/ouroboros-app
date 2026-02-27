// <copyright file="FileWriteTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text.Json;

/// <summary>
/// Write to files.
/// </summary>
internal class FileWriteTool : ITool
{
    public string Name => "write_file";
    public string Description => "Write content to a file. Input: JSON {\"path\":\"...\", \"content\":\"...\", \"append\":false}";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(input);
            var rawPath = args.GetProperty("path").GetString() ?? "";
            var content = args.GetProperty("content").GetString() ?? "";
            var append = args.TryGetProperty("append", out var appendEl) && appendEl.GetBoolean();

            var path = PathSanitizer.Sanitize(rawPath);

            // Ensure directory exists
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (append)
                await File.AppendAllTextAsync(path, content, ct);
            else
                await File.WriteAllTextAsync(path, content, ct);

            return Result<string, string>.Success($"Written {content.Length} chars to {path}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<string, string>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure(ex.Message);
        }
    }
}
