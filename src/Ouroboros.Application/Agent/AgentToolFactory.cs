// <copyright file="AgentToolFactory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Agent;

/// <summary>
/// Builds the dictionary of tools available to the AutoAgent during its reasoning loop.
/// Each tool is a <c>Func&lt;string, CliPipelineState, Task&lt;string&gt;&gt;</c> keyed by name.
/// </summary>
public static class AgentToolFactory
{
    /// <summary>
    /// Creates the full set of agent tools wired against the given pipeline state.
    /// </summary>
    public static Dictionary<string, Func<string, CliPipelineState, Task<string>>> Build(CliPipelineState state)
    {
        return new Dictionary<string, Func<string, CliPipelineState, Task<string>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["read_file"] = ReadFileAsync,
            ["write_file"] = WriteFileAsync,
            ["edit_file"] = EditFileAsync,
            ["list_dir"] = ListDirAsync,
            ["search_files"] = SearchFilesAsync,
            ["run_command"] = RunCommandAsync,
            ["vector_search"] = VectorSearchAsync,
            ["think"] = ThinkAsync,
            ["ask_user"] = AskUserAsync,
        };
    }

    private static async Task<string> ReadFileAsync(string args, CliPipelineState s)
    {
        var path = ParseToolArg(args, "path") ?? args.Trim().Trim('"', '\'');
        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            var content = await File.ReadAllTextAsync(path);
            if (content.Length > 10000)
                content = content[..10000] + $"\n\n... [truncated, {content.Length} total chars]";
            return content;
        }
        catch (Exception ex)
        {
            return $"Error reading file: {ex.Message}";
        }
    }

    private static async Task<string> WriteFileAsync(string args, CliPipelineState s)
    {
        var path = ParseToolArg(args, "path");
        var content = ParseToolArg(args, "content");

        if (string.IsNullOrEmpty(path) || content == null)
            return "Error: Required args: path, content";

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(path, content);
            return $"Successfully wrote {content.Length} chars to {path}";
        }
        catch (Exception ex)
        {
            return $"Error writing file: {ex.Message}";
        }
    }

    private static async Task<string> EditFileAsync(string args, CliPipelineState s)
    {
        var path = ParseToolArg(args, "path");
        var oldText = ParseToolArg(args, "old");
        var newText = ParseToolArg(args, "new");

        if (string.IsNullOrEmpty(path) || oldText == null || newText == null)
            return "Error: Required args: path, old, new";

        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            var content = await File.ReadAllTextAsync(path);
            if (!content.Contains(oldText))
                return $"Error: Old text not found in file. Make sure to include enough context.";

            var newContent = content.Replace(oldText, newText);
            await File.WriteAllTextAsync(path, newContent);
            return $"Successfully edited {path}";
        }
        catch (Exception ex)
        {
            return $"Error editing file: {ex.Message}";
        }
    }

    private static Task<string> ListDirAsync(string args, CliPipelineState s)
    {
        var path = ParseToolArg(args, "path") ?? args.Trim().Trim('"', '\'');
        if (string.IsNullOrEmpty(path)) path = ".";

        if (!Directory.Exists(path))
            return Task.FromResult($"Error: Directory not found: {path}");

        try
        {
            var entries = new List<string>();
            foreach (var dir in Directory.GetDirectories(path).Take(50))
                entries.Add(Path.GetFileName(dir) + "/");
            foreach (var file in Directory.GetFiles(path).Take(100))
                entries.Add(Path.GetFileName(file));

            return Task.FromResult(string.Join("\n", entries));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error listing directory: {ex.Message}");
        }
    }

    private static async Task<string> SearchFilesAsync(string args, CliPipelineState s)
    {
        var query = ParseToolArg(args, "query") ?? args.Trim().Trim('"', '\'');
        var path = ParseToolArg(args, "path") ?? ".";
        var pattern = ParseToolArg(args, "pattern") ?? "*.cs";

        if (string.IsNullOrEmpty(query))
            return "Error: Required arg: query";

        try
        {
            var results = new List<string>();
            foreach (var file in Directory.GetFiles(path, pattern, SearchOption.AllDirectories).Take(100))
            {
                var content = await File.ReadAllTextAsync(file);
                if (content.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    var lines = content.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add($"{file}:{i + 1}: {lines[i].Trim()}");
                            if (results.Count >= 20) break;
                        }
                    }
                }

                if (results.Count >= 20) break;
            }

            return results.Count > 0
                ? string.Join("\n", results)
                : "No matches found";
        }
        catch (Exception ex)
        {
            return $"Error searching: {ex.Message}";
        }
    }

    private static async Task<string> RunCommandAsync(string args, CliPipelineState s)
    {
        var command = ParseToolArg(args, "command") ?? args.Trim().Trim('"', '\'');

        if (string.IsNullOrEmpty(command))
            return "Error: Required arg: command";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null) return "Error: Failed to start process";

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var result = new StringBuilder();
            if (!string.IsNullOrEmpty(output)) result.AppendLine(output);
            if (!string.IsNullOrEmpty(error)) result.AppendLine($"STDERR: {error}");
            result.AppendLine($"Exit code: {process.ExitCode}");

            var text = result.ToString();
            if (text.Length > 5000) text = text[..5000] + "\n... [truncated]";
            return text;
        }
        catch (Exception ex)
        {
            return $"Error running command: {ex.Message}";
        }
    }

    private static async Task<string> VectorSearchAsync(string args, CliPipelineState s)
    {
        var query = ParseToolArg(args, "query") ?? args.Trim().Trim('"', '\'');

        if (string.IsNullOrEmpty(query))
            return "Error: Required arg: query";

        if (s.VectorStore == null && s.Branch.Store == null)
            return "Error: No vector store available. Use UseQdrant first.";

        var store = s.VectorStore ?? s.Branch.Store;

        try
        {
            var embedding = await s.Embed.CreateEmbeddingsAsync(query);
            var results = await store.GetSimilarDocumentsAsync(embedding, 5);

            if (results.Count == 0) return "No similar documents found";

            var sb = new StringBuilder();
            int i = 0;
            foreach (var doc in results)
            {
                i++;
                sb.AppendLine($"[{i}] {StringHelpers.TruncateForDisplay(doc.PageContent, 500)}");
                sb.AppendLine("---");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error searching vectors: {ex.Message}";
        }
    }

    private static Task<string> ThinkAsync(string args, CliPipelineState s)
    {
        return Task.FromResult($"Thought recorded: {args}");
    }

    private static Task<string> AskUserAsync(string args, CliPipelineState s)
    {
        Console.WriteLine($"\n[AutoAgent] Question for user: {args}");
        Console.Write("[AutoAgent] Your response (or press Enter to skip): ");
        var response = Console.ReadLine();
        return Task.FromResult(string.IsNullOrEmpty(response) ? "User skipped the question" : response);
    }

    /// <summary>
    /// Extracts a named argument from a JSON string.
    /// Returns <c>null</c> when the input is not valid JSON or the property is missing.
    /// </summary>
    internal static string? ParseToolArg(string json, string argName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(argName, out var prop))
            {
                return prop.ValueKind == JsonValueKind.String
                    ? prop.GetString()
                    : prop.GetRawText();
            }
        }
        catch
        {
            // Not valid JSON, return null
        }

        return null;
    }
}
