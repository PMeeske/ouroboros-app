// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text;
using System.Text.RegularExpressions;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

/// <summary>
/// Pipe processing subsystem: command chaining via | syntax, [PIPE:] detection,
/// and non-interactive batch/exec/pipe-mode execution.
/// </summary>
public sealed partial class PipeProcessingSubsystem : IPipeProcessingSubsystem
{
    public string Name => "PipeProcessing";
    public bool IsInitialized { get; private set; }

    private OuroborosConfig _config = null!;

    /// <summary>
    /// Set by agent during WireCrossSubsystemDependencies to the agent's ProcessInputAsync.
    /// Required before RunNonInteractiveModeAsync or ProcessInputWithPipingAsync can be called.
    /// </summary>
    internal Func<string, Task<string>> ProcessInputFunc { get; set; } =
        _ => Task.FromResult("PipeProcessing not yet wired.");

    public Task InitializeAsync(SubsystemInitContext ctx)
    {
        _config = ctx.Config;
        IsInitialized = true;
        ctx.Output.RecordInit("PipeProcessing", true);
        return Task.CompletedTask;
    }

    public async Task<string> ProcessInputWithPipingAsync(string input, int maxPipeDepth = 5)
    {
        var segments = ParsePipeSegments(input);

        if (segments.Count <= 1)
        {
            var response = await ProcessInputFunc(input);
            return await ExecuteModelPipeCommandsAsync(response, maxPipeDepth);
        }

        string? lastOutput = null;
        var allOutputs = new List<string>();

        for (int i = 0; i < segments.Count && i < maxPipeDepth; i++)
        {
            var segment = segments[i].Trim();
            if (string.IsNullOrWhiteSpace(segment)) continue;

            var commandToRun = segment;
            if (lastOutput != null)
            {
                commandToRun = commandToRun
                    .Replace("$PIPE", lastOutput)
                    .Replace("$_", lastOutput);

                if (!segment.Contains("$PIPE") && !segment.Contains("$_"))
                    commandToRun = $"Given this context:\n---\n{lastOutput}\n---\n{segment}";
            }

            try
            {
                lastOutput = await ProcessInputFunc(commandToRun);
                allOutputs.Add($"[Step {i + 1}: {segment[..Math.Min(30, segment.Length)]}...]\n{lastOutput}");
            }
            catch (InvalidOperationException ex)
            {
                allOutputs.Add($"[Step {i + 1} ERROR: {ex.Message}]");
                break;
            }
        }

        return lastOutput ?? string.Join("\n\n", allOutputs);
    }

    public async Task RunNonInteractiveModeAsync()
    {
        var commands = new List<string>();

        if (!string.IsNullOrWhiteSpace(_config.ExecCommand))
        {
            commands.Add(_config.ExecCommand);
        }
        else if (!string.IsNullOrWhiteSpace(_config.BatchFile))
        {
            if (!File.Exists(_config.BatchFile))
            {
                OutputError($"Batch file not found: {_config.BatchFile}");
                return;
            }
            commands.AddRange(await File.ReadAllLinesAsync(_config.BatchFile));
        }
        else if (_config.PipeMode || Console.IsInputRedirected)
        {
            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    commands.Add(line);
            }
        }

        string? lastOutput = null;
        foreach (var rawCmd in commands)
        {
            var cmd = rawCmd.Trim();
            if (string.IsNullOrWhiteSpace(cmd) || cmd.StartsWith("#")) continue;

            var pipeSegments = ParsePipeSegments(cmd);

            foreach (var segment in pipeSegments)
            {
                var commandToRun = segment.Trim();

                if (lastOutput != null)
                {
                    commandToRun = commandToRun
                        .Replace("$PIPE", lastOutput)
                        .Replace("$_", lastOutput);

                    if (segment.TrimStart().StartsWith("|"))
                        commandToRun = $"{lastOutput}\n---\n{commandToRun.TrimStart().TrimStart('|').Trim()}";
                }

                if (string.IsNullOrWhiteSpace(commandToRun)) continue;

                try
                {
                    var response = await ProcessInputFunc(commandToRun);
                    lastOutput = response;
                    OutputResponse(commandToRun, response);
                }
                catch (InvalidOperationException ex)
                {
                    OutputError($"Error processing '{commandToRun}': {ex.Message}");
                    if (_config.ExitOnError)
                        return;
                    lastOutput = null;
                }
            }
        }
    }

    private async Task<string> ExecuteModelPipeCommandsAsync(string response, int maxDepth)
    {
        if (maxDepth <= 0) return response;

        var matches = PipePatternRegex().Matches(response);

        if (matches.Count == 0) return response;

        var result = response;
        foreach (Match match in matches)
        {
            var pipeCommand = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(pipeCommand)) continue;

            try
            {
                AnsiConsole.MarkupLine($"[rgb(148,103,189)]  🔗 Executing pipe: {Markup.Escape(pipeCommand[..Math.Min(50, pipeCommand.Length)])}...[/]");

                var pipeResult = await ProcessInputWithPipingAsync(pipeCommand.Trim(), maxDepth - 1);
                result = result.Replace(match.Value, $"\n📤 Pipe Result:\n{pipeResult}\n");
            }
            catch (InvalidOperationException ex)
            {
                result = result.Replace(match.Value, $"\n❌ Pipe Error: {ex.Message}\n");
            }
        }

        return result;
    }

    private static List<string> ParsePipeSegments(string command)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        bool inQuote = false;
        char quoteChar = '"';

        for (int i = 0; i < command.Length; i++)
        {
            char c = command[i];

            if ((c == '"' || c == '\'') && (i == 0 || command[i - 1] != '\\'))
            {
                if (!inQuote)
                {
                    inQuote = true;
                    quoteChar = c;
                }
                else if (c == quoteChar)
                {
                    inQuote = false;
                }
                current.Append(c);
                continue;
            }

            if (c == '|' && !inQuote)
            {
                var segment = current.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(segment))
                    segments.Add(segment);
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        var final = current.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(final))
            segments.Add(final);

        return segments;
    }

    private void OutputResponse(string command, string response)
    {
        if (_config.JsonOutput)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                command,
                response,
                timestamp = DateTime.UtcNow,
                success = true
            });
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine(response);
        }
    }

    private void OutputError(string message)
    {
        if (_config.JsonOutput)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                error = message,
                timestamp = DateTime.UtcNow,
                success = false
            });
            Console.WriteLine(json);
        }
        else
        {
            Console.Error.WriteLine($"ERROR: {message}");
        }
    }

    [GeneratedRegex(@"\[PIPE:\s*(.+?)\]|```pipe\s*\n(.+?)\n```", RegexOptions.Singleline)]
    private static partial Regex PipePatternRegex();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
