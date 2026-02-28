// <copyright file="OuroborosAgent.Tools.Claude.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using Ouroboros.Abstractions.Monads;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Mediator;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Claude CLI tool registration, subprocess helpers, skill/tool mediator wrappers.
/// </summary>
public sealed partial class OuroborosAgent
{
    private Task<string> ListSkillsAsync()
        => _mediator.Send(new ListSkillsRequest());

    private Task<string> LearnTopicAsync(string topic)
        => _mediator.Send(new LearnTopicRequest(topic));

    internal static string SanitizeSkillName(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("(", "")
            .Replace(")", "");
    }

    private Task<string> CreateToolAsync(string toolName)
        => _mediator.Send(new CreateToolRequest(toolName));

    private Task<string> UseToolAsync(string toolName, string? input)
        => _mediator.Send(new UseToolRequest(toolName, input));

    private Task<string> RunSkillAsync(string skillName)
        => _mediator.Send(new RunSkillRequest(skillName));

    private Task<string> SuggestSkillsAsync(string goal)
        => _mediator.Send(new SuggestSkillsRequest(goal));

    /// <summary>
    /// Registers Claude CLI-backed meta-tools for Iaret. Each tool shells out to the
    /// real <c>claude</c> executable (npm @anthropic-ai/claude-code), auto-discovered
    /// from PATH or the local VS Code extension bundle.
    ///   • claude_plan        — run <c>claude --print</c> to generate a structured plan
    ///   • claude_ask         — run <c>claude --print</c> to get a Claude answer
    ///   • claude_bypass_code — run <c>claude --dangerously-skip-permissions --print</c>
    /// </summary>
    private void RegisterClaudeStyleTools()
    {
        // -- claude_plan --
        var planTool = new DelegateTool(
            "claude_plan",
            "Use the Claude CLI to generate a detailed step-by-step plan before executing a " +
            "complex or multi-step task. Use this when a task requires several actions or " +
            "could have side-effects and you want a structured approach. " +
            "Input: describe the goal. " +
            "Returns the plan from Claude. The user can approve, revise, or cancel before you proceed.",
            async (string input, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return Result<string, string>.Failure("Input required: describe the goal to plan.");

                var prompt =
                    "Create a detailed, actionable step-by-step plan for the following task. " +
                    "Format as numbered steps with sub-steps where needed. Be specific.\n\nTask: " + input;

                var result = await RunClaudeAsync(["--print", prompt, "--output-format", "text"], ct);

                if (result.IsSuccess)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Panel(Markup.Escape(result.Value))
                        .Header("[bold cyan]  Claude Plan  [/]")
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.Cyan1));
                    AnsiConsole.MarkupLine(OuroborosTheme.Dim(
                        "  Say 'proceed' or 'approve' to execute, or give feedback to revise."));
                    AnsiConsole.WriteLine();
                }

                return result;
            });

        _tools = _tools.WithTool(planTool);

        // -- claude_ask --
        var askTool = new DelegateTool(
            "claude_ask",
            "Send a question or prompt to the Claude CLI and return its answer. " +
            "Use this when you need Claude's reasoning, knowledge, or a second opinion on something. " +
            "Input: the question or prompt to send. " +
            "Returns Claude's response.",
            async (string input, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return Result<string, string>.Failure("Input required: provide a question or prompt.");

                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Spectre.Console.Rule("[bold cyan]Claude CLI[/]").RuleStyle("cyan dim"));

                var result = await RunClaudeAsync(["--print", input, "--output-format", "text"], ct);

                AnsiConsole.WriteLine();
                return result;
            });

        _tools = _tools.WithTool(askTool);

        // -- claude_bypass_code --
        var bypassTool = new DelegateTool(
            "claude_bypass_code",
            "Run a task via the Claude CLI with --dangerously-skip-permissions, which allows " +
            "Claude to execute code, write files, and run shell commands without per-action approval. " +
            "Use this when you need Claude to autonomously complete a coding or file task. " +
            "Input: the task or prompt to execute with full permissions. " +
            "Returns Claude's output.",
            async (string input, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return Result<string, string>.Failure("Input required: provide the task for Claude to execute.");

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(OuroborosTheme.Warn("  ⚡ Running Claude with --dangerously-skip-permissions…"));

                var result = await RunClaudeAsync(
                    ["--dangerously-skip-permissions", "--print", input, "--output-format", "text"], ct);

                AnsiConsole.WriteLine();
                return result;
            });

        _tools = _tools.WithTool(bypassTool);

        // -- claude_edit --
        var editTool = new DelegateTool(
            "claude_edit",
            "Use the Claude CLI to make targeted code edits to local files. " +
            "Claude is granted Read, Edit, Write, and Bash tool access so it can " +
            "read the file, apply the change, and verify the result — without full bypass. " +
            "Input: describe exactly what to change and in which file(s). " +
            "Returns Claude's edit summary. Call claude_continue afterwards to resume context.",
            async (string input, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return Result<string, string>.Failure("Input required: describe the edit to make.");

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("  ✏  Claude editing…"));

                var result = await RunClaudeAsync(
                    ["--allowedTools", "Read,Edit,Write,Bash", "--print", input, "--output-format", "text"], ct);

                AnsiConsole.WriteLine();
                return result;
            });

        _tools = _tools.WithTool(editTool);

        // -- claude_continue --
        var continueTool = new DelegateTool(
            "claude_continue",
            "Resume the most recent Claude CLI conversation after local code changes, " +
            "so you do not have to exit and restart. " +
            "Use this after claude_edit or any manual file change to hand context back to Claude. " +
            "Input: optional follow-up message or leave empty to just continue. " +
            "Optionally prefix with a session ID and a space to resume a specific session: '<id> <message>'. " +
            "Returns Claude's continued response.",
            async (string input, CancellationToken ct) =>
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("  ↩  Resuming Claude session…"));

                // Allow optional "session-id message" prefix
                string? sessionId = null;
                var message = (input ?? string.Empty).Trim();
                var spaceIdx = message.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    var candidate = message[..spaceIdx];
                    // Session IDs are long hex/UUID-ish strings — heuristic: no spaces, 8+ chars, no common words
                    if (candidate.Length >= 8 && !candidate.Contains('.') && !candidate.Contains('/'))
                    {
                        sessionId = candidate;
                        message = message[(spaceIdx + 1)..].Trim();
                    }
                }

                List<string> args;
                if (sessionId is not null)
                {
                    args = ["--resume", sessionId, "--print", message, "--output-format", "text"];
                }
                else if (string.IsNullOrWhiteSpace(message))
                {
                    args = ["--continue", "--print", "Please continue.", "--output-format", "text"];
                }
                else
                {
                    args = ["--continue", "--print", message, "--output-format", "text"];
                }

                var result = await RunClaudeAsync(args, ct);
                AnsiConsole.WriteLine();
                return result;
            });

        _tools = _tools.WithTool(continueTool);

        var claudePath = ResolveClaudeExecutable() ?? "claude (not found — install @anthropic-ai/claude-code)";
        _output.RecordInit("Claude CLI Tools", true,
            $"claude_plan + claude_ask + claude_bypass_code + claude_edit + claude_continue → {claudePath}");
    }

    // -- Claude CLI subprocess helpers --

    /// <summary>
    /// Invokes the <c>claude</c> executable with the given argument list.
    /// Tries PATH first, then the VS Code extension bundle.
    /// </summary>
    private static async Task<Result<string, string>> RunClaudeAsync(
        IReadOnlyList<string> args,
        CancellationToken ct = default)
    {
        var exe = ResolveClaudeExecutable();
        if (exe is null)
            return Result<string, string>.Failure(
                "claude CLI not found. Install with: npm install -g @anthropic-ai/claude-code");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process is null)
                return Result<string, string>.Failure("Failed to start claude process.");

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return process.ExitCode == 0
                ? Result<string, string>.Success(stdout.Trim())
                : Result<string, string>.Failure(
                    string.IsNullOrWhiteSpace(stderr) ? $"claude exited with code {process.ExitCode}" : stderr.Trim());
        }
        catch (OperationCanceledException)
        {
            return Result<string, string>.Failure("Cancelled.");
        }
        catch (InvalidOperationException ex)
        {
            return Result<string, string>.Failure($"claude process error: {ex.Message}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return Result<string, string>.Failure($"claude process error: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves the <c>claude</c> executable path.
    /// Prefers the system PATH entry; falls back to the VS Code extension bundle.
    /// </summary>
    private static string? ResolveClaudeExecutable()
    {
        // 1. Try PATH (works when installed via npm install -g)
        try
        {
            var probePsi = new ProcessStartInfo
            {
                FileName = "claude",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            probePsi.ArgumentList.Add("--version");
            using var probe = Process.Start(probePsi);
            probe?.WaitForExit(2000);
            if (probe?.ExitCode == 0) return "claude";
        }
        catch (InvalidOperationException) { /* fall through — claude CLI not found */ }
        catch (System.ComponentModel.Win32Exception) { /* fall through — claude CLI not found */ }

        // 2. VS Code extension bundle (Windows: anthropic.claude-code-*)
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var extDir = Path.Combine(home, ".vscode", "extensions");
        if (Directory.Exists(extDir))
        {
            var claudeDir = Directory.GetDirectories(extDir, "anthropic.claude-code-*")
                .OrderByDescending(d => d)
                .FirstOrDefault();

            if (claudeDir is not null)
            {
                foreach (var candidate in new[] { "claude.exe", "claude" })
                {
                    var full = Path.Combine(claudeDir, "resources", "native-binary", candidate);
                    if (File.Exists(full)) return full;
                }
            }
        }

        return null;
    }
}
