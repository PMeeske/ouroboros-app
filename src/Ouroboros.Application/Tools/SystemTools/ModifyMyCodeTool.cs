// <copyright file="ModifyMyCodeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text.Json;

/// <summary>
/// Modify my own source code - true self-modification capability.
/// </summary>
internal class ModifyMyCodeTool : ITool
{
    public string Name => "modify_my_code";
    public string Description => "Modify my own source code. This is a powerful self-modification capability. Input JSON: {\"file\": \"relative/path/to/file.cs\", \"search\": \"exact text to find\", \"replace\": \"replacement text\"}. Always backup important files first!";
    public string? JsonSchema => """{"type":"object","properties":{"file":{"type":"string"},"search":{"type":"string"},"replace":{"type":"string"}},"required":["file","search","replace"]}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(input);
            var file = args.GetProperty("file").GetString() ?? "";
            var search = args.GetProperty("search").GetString() ?? "";
            var replace = args.GetProperty("replace").GetString() ?? "";

            if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(search))
            {
                return Result<string, string>.Failure("File path and search text are required.");
            }

            // Resolve path
            var filePath = Path.IsPathRooted(file) ? file : Path.Combine(Environment.CurrentDirectory, file);

            // Path sanitization
            filePath = PathSanitizer.Sanitize(filePath);

            if (!File.Exists(filePath))
            {
                return Result<string, string>.Failure($"File not found: {filePath}");
            }

            // Safety check - only allow modifying .cs, .json, .md, .txt files
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var allowedExtensions = new[] { ".cs", ".json", ".md", ".txt", ".yaml", ".yml", ".xml", ".config" };
            if (!allowedExtensions.Contains(ext))
            {
                return Result<string, string>.Failure($"Cannot modify {ext} files. Allowed: {string.Join(", ", allowedExtensions)}");
            }

            var content = await File.ReadAllTextAsync(filePath, ct);

            if (!content.Contains(search))
            {
                return Result<string, string>.Failure($"Search text not found in {file}. Make sure the search string matches exactly.");
            }

            // Create backup
            var backupPath = filePath + $".backup.{DateTime.Now:yyyyMMdd_HHmmss}";
            await File.WriteAllTextAsync(backupPath, content, ct);

            // Perform replacement
            var newContent = content.Replace(search, replace);
            await File.WriteAllTextAsync(filePath, newContent, ct);

            var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, filePath);
            return Result<string, string>.Success($"Modified **{relativePath}**\n\nBackup saved to: {Path.GetFileName(backupPath)}\n\nNote: Changes require rebuild (`dotnet build`) to take effect.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<string, string>.Failure(ex.Message);
        }
        catch (IOException ex)
        {
            return Result<string, string>.Failure($"Self-modification failed: {ex.Message}");
        }
    }
}
