// <copyright file="AgentToolFactory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ouroboros.Application.Tools.SystemTools;

namespace Ouroboros.Application.Agent;

/// <summary>
/// Builds the dictionary of tools available to the AutoAgent during its reasoning loop.
/// Each tool is a <c>Func&lt;string, CliPipelineState, Task&lt;string&gt;&gt;</c> keyed by name.
/// </summary>
public static class AgentToolFactory
{
    /// <summary>
    /// Maximum time a <c>run_command</c> process is allowed to run before being killed.
    /// </summary>
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Regex patterns that match dangerous shell commands. If any pattern matches
    /// the command string, execution is blocked before <c>Process.Start</c>.
    /// Modelled after <see cref="OpenClaw.PcNode.PcNodeSecurityConfig.BlockedShellPatterns"/>.
    /// </summary>
    private static readonly Regex[] BlockedCommandPatterns =
    [
        new(@"rm\s+-rf\s+/", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"del\s+/[sfq].*\s+[a-zA-Z]:\\", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"format\s+[a-zA-Z]:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"mkfs\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"shutdown\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"reboot\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@":\(\)\s*\{\s*:\s*\|\s*:\s*&\s*\}\s*;\s*:", RegexOptions.Compiled),  // fork bomb
        new(@"dd\s+if=", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@">\s*/dev/sda", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"reg\s+delete", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"net\s+user", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"netsh\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"diskpart", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"bcdedit", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    /// <summary>
    /// Checks a command string against <see cref="BlockedCommandPatterns"/> and returns
    /// an error message if the command is blocked, or <c>null</c> if it is safe.
    /// </summary>
    internal static string? CheckCommandSafety(string command)
    {
        foreach (var pattern in BlockedCommandPatterns)
        {
            if (pattern.IsMatch(command))
                return $"Error: Command blocked by security policy (matched pattern: {pattern})";
        }

        return null;
    }

    /// <summary>
    /// Tool metadata used to auto-generate prompt descriptions.
    /// </summary>
    public static readonly IReadOnlyList<AgentToolDescriptor> ToolDescriptors =
    [
        new("read_file", "Read the contents of a file.", """{"path": "path/to/file.cs"}"""),
        new("write_file", "Create or overwrite a file with new content.", """{"path": "path/to/file.cs", "content": "file contents here"}"""),
        new("edit_file", "Replace specific text in a file. Include enough context to uniquely identify the location.", """{"path": "path/to/file.cs", "old": "text to replace", "new": "replacement text"}"""),
        new("list_dir", "List contents of a directory.", """{"path": "path/to/directory"}"""),
        new("search_files", "Search for text across files.", """{"query": "search text", "path": ".", "pattern": "*.cs"}"""),
        new("run_command", "Execute a shell command.", """{"command": "dotnet build"}"""),
        new("vector_search", "Search the vector store for similar documents (requires UseQdrant).", """{"query": "semantic search query"}"""),
        new("think", "Record your thoughts/planning (no external action).", """{"thought": "I need to first..."}"""),
        new("ask_user", "Ask the user a clarifying question.", """{"question": "What file should I modify?"}"""),
    ];

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
        var rawPath = ParseToolArg(args, "path") ?? args.Trim().Trim('"', '\'');
        string path;
        try { path = PathSanitizer.Sanitize(rawPath); }
        catch (UnauthorizedAccessException ex) { return $"Error: {ex.Message}"; }

        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            var content = await File.ReadAllTextAsync(path, s.CancellationToken);
            if (content.Length > 10000)
                content = content[..10000] + $"\n\n... [truncated, {content.Length} total chars]";
            return content;
        }
        catch (OperationCanceledException)
        {
            return "Error: Operation cancelled";
        }
        catch (IOException ex)
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

        // Security gate: sanitize and validate the file path
        try
        {
            path = PathSanitizer.Sanitize(path);
        }
        catch (UnauthorizedAccessException ex)
        {
            return $"Error: {ex.Message}";
        }

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(path, content, s.CancellationToken);
            return $"Successfully wrote {content.Length} chars to {path}";
        }
        catch (OperationCanceledException)
        {
            return "Error: Operation cancelled";
        }
        catch (IOException ex)
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

        // Security gate: sanitize and validate the file path
        try
        {
            path = PathSanitizer.Sanitize(path);
        }
        catch (UnauthorizedAccessException ex)
        {
            return $"Error: {ex.Message}";
        }

        if (!File.Exists(path))
            return $"Error: File not found: {path}";

        try
        {
            var content = await File.ReadAllTextAsync(path, s.CancellationToken);
            if (!content.Contains(oldText))
                return $"Error: Old text not found in file. Make sure to include enough context.";

            var newContent = content.Replace(oldText, newText);
            await File.WriteAllTextAsync(path, newContent, s.CancellationToken);
            return $"Successfully edited {path}";
        }
        catch (OperationCanceledException)
        {
            return "Error: Operation cancelled";
        }
        catch (IOException ex)
        {
            return $"Error editing file: {ex.Message}";
        }
    }

    private static Task<string> ListDirAsync(string args, CliPipelineState s)
    {
        var rawPath = ParseToolArg(args, "path") ?? args.Trim().Trim('"', '\'');
        if (string.IsNullOrEmpty(rawPath)) rawPath = ".";

        string path;
        try { path = PathSanitizer.Sanitize(rawPath); }
        catch (UnauthorizedAccessException ex) { return Task.FromResult($"Error: {ex.Message}"); }

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
        catch (IOException ex)
        {
            return Task.FromResult($"Error listing directory: {ex.Message}");
        }
    }

    private static async Task<string> SearchFilesAsync(string args, CliPipelineState s)
    {
        var query = ParseToolArg(args, "query") ?? args.Trim().Trim('"', '\'');
        var rawPath = ParseToolArg(args, "path") ?? ".";
        var pattern = ParseToolArg(args, "pattern") ?? "*.cs";

        if (string.IsNullOrEmpty(query))
            return "Error: Required arg: query";

        string path;
        try { path = PathSanitizer.Sanitize(rawPath); }
        catch (UnauthorizedAccessException ex) { return $"Error: {ex.Message}"; }

        try
        {
            const long maxFileSize = 1024 * 1024; // Skip files > 1 MB
            const int maxResults = 20;

            var results = new List<string>();
            foreach (var file in Directory.GetFiles(path, pattern, SearchOption.AllDirectories).Take(200))
            {
                s.CancellationToken.ThrowIfCancellationRequested();

                // Skip files that are too large
                var fileInfo = new FileInfo(file);
                if (fileInfo.Length > maxFileSize) continue;

                // Stream lines instead of reading entire file into memory
                int lineNum = 0;
                await foreach (var line in File.ReadLinesAsync(file, s.CancellationToken))
                {
                    lineNum++;
                    if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add($"{file}:{lineNum}: {line.Trim()}");
                        if (results.Count >= maxResults) break;
                    }
                }

                if (results.Count >= maxResults) break;
            }

            return results.Count > 0
                ? string.Join("\n", results)
                : "No matches found";
        }
        catch (OperationCanceledException)
        {
            return "Error: Search cancelled";
        }
        catch (IOException ex)
        {
            return $"Error searching: {ex.Message}";
        }
    }

    private static async Task<string> RunCommandAsync(string args, CliPipelineState s)
    {
        var command = ParseToolArg(args, "command") ?? args.Trim().Trim('"', '\'');

        if (string.IsNullOrEmpty(command))
            return "Error: Required arg: command";

        // Security gate: block dangerous command patterns before execution
        var blockReason = CheckCommandSafety(command);
        if (blockReason != null)
            return blockReason;

        try
        {
            var (shell, shellArgs) = GetShellCommand(command);
            var psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = shellArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null) return "Error: Failed to start process";

            // Enforce a timeout so runaway commands don't block the agent loop
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(s.CancellationToken);
            timeoutCts.CancelAfter(CommandTimeout);

            try
            {
                var output = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                var error = await process.StandardError.ReadToEndAsync(timeoutCts.Token);
                await process.WaitForExitAsync(timeoutCts.Token);

                var result = new StringBuilder();
                if (!string.IsNullOrEmpty(output)) result.AppendLine(output);
                if (!string.IsNullOrEmpty(error)) result.AppendLine($"STDERR: {error}");
                result.AppendLine($"Exit code: {process.ExitCode}");

                var text = result.ToString();
                if (text.Length > 5000) text = text[..5000] + "\n... [truncated]";
                return text;
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { /* process already exited */ }
                return s.CancellationToken.IsCancellationRequested
                    ? "Error: Command cancelled"
                    : $"Error: Command timed out after {CommandTimeout.TotalSeconds}s and was killed";
            }
        }
        catch (InvalidOperationException ex)
        {
            return $"Error running command: {ex.Message}";
        }
        catch (System.ComponentModel.Win32Exception ex)
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
        catch (HttpRequestException ex)
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
    /// Returns the platform-appropriate shell and argument string for executing a command.
    /// On Windows: uses <c>cmd.exe /C</c>. On Linux/macOS: uses <c>/bin/sh -c</c>.
    /// </summary>
    internal static (string Shell, string Arguments) GetShellCommand(string command)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ("cmd.exe", $"/C {command}");

        return ("/bin/sh", $"-c \"{command.Replace("\"", "\\\"")}\"");
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

/// <summary>
/// Describes an agent tool for prompt generation. Kept in sync with <see cref="AgentToolFactory.Build"/>.
/// </summary>
/// <param name="Name">Tool name as used in JSON tool calls.</param>
/// <param name="Description">Human-readable description for the LLM.</param>
/// <param name="ArgsExample">JSON example of the expected arguments.</param>
public sealed record AgentToolDescriptor(string Name, string Description, string ArgsExample);
