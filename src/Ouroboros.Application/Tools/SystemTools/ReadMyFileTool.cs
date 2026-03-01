// <copyright file="ReadMyFileTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;

/// <summary>
/// Read a specific file from my own codebase.
/// </summary>
internal class ReadMyFileTool : ITool
{
    public string Name => "read_my_file";
    public string Description => "Read a specific file from my own source code. Input: relative or absolute file path (e.g., 'src/Ouroboros.CLI/Commands/ImmersiveMode.cs').";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        var filePath = input.Trim().Trim('"');

        // Try to resolve relative path
        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(Environment.CurrentDirectory, filePath);
        }

        // Security gate: sanitize and validate the file path
        try
        {
            filePath = PathSanitizer.Sanitize(filePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<string, string>.Failure($"Error: {ex.Message}");
        }

        if (!File.Exists(filePath))
        {
            return Result<string, string>.Failure($"File not found: {filePath}");
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, ct);

            // Truncate if too long
            if (content.Length > 8000)
            {
                content = content.Substring(0, 8000) + "\n\n... [truncated - file too long] ...";
            }

            var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, filePath);
            var sb = new StringBuilder();
            sb.AppendLine($"  **{relativePath}**");
            sb.AppendLine("```");
            sb.AppendLine(content);
            sb.AppendLine("```");

            return Result<string, string>.Success(sb.ToString());
        }
        catch (IOException ex)
        {
            return Result<string, string>.Failure($"Failed to read file: {ex.Message}");
        }
    }
}
