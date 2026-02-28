// <copyright file="FileReadTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text.Json;

/// <summary>
/// Read file contents.
/// </summary>
internal class FileReadTool : ITool
{
    public string Name => "read_file";
    public string Description => "Read file contents. Input: JSON {\"path\":\"...\", \"lines\":100} or just path";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            string rawPath;
            int maxLines = 500;

            if (input.TrimStart().StartsWith("{"))
            {
                var args = JsonSerializer.Deserialize<JsonElement>(input);
                rawPath = args.GetProperty("path").GetString() ?? "";
                if (args.TryGetProperty("lines", out var linesEl))
                    maxLines = linesEl.GetInt32();
            }
            else
            {
                rawPath = input.Trim().Trim('"');
            }

            var path = PathSanitizer.Sanitize(rawPath);

            if (!File.Exists(path))
                return Result<string, string>.Failure($"File not found: {path}");

            var lines = await File.ReadAllLinesAsync(path, ct);
            if (lines.Length > maxLines)
            {
                var result = string.Join("\n", lines.Take(maxLines));
                return Result<string, string>.Success($"{result}\n\n... [{lines.Length - maxLines} more lines truncated]");
            }

            return Result<string, string>.Success(string.Join("\n", lines));
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
