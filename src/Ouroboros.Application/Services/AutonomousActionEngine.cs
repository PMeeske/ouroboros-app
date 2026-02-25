// <copyright file="AutonomousActionEngine.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

/// <summary>
/// Runs a background loop every <see cref="Interval"/> (default 3 minutes) and lets
/// Iaret autonomously decide on and execute a real step-action — a DSL pipe command,
/// a tool call, or any full agent pipeline expression. Fires <see cref="OnAction"/>
/// with the action description and its execution result.
///
/// Distinct from <see cref="AutonomousMind"/>:
///   • AutonomousMind  → inner curiosity loop, continuous research and tool execution
///   • ActionEngine    → discrete step-actions (pipe DSL / tools) every N minutes, surfaced as [Autonomous] 💬
///
/// Enabled by default; does not require EnableMind.
///
/// Decision format the LLM must output:
///   COMMAND: &lt;pipe DSL or tool call — executed via the full agent pipeline&gt;
///   REASON:  &lt;one sentence — what Iaret intends to do and why&gt;
///
/// If no COMMAND is found (plain text response), the text is surfaced directly as the action statement.
/// </summary>
public sealed class AutonomousActionEngine : IDisposable
{
    /// <summary>How often to run a step-action cycle. Default: 3 minutes.</summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// LLM delegate for the decision prompt.
    /// Must be set before <see cref="Start"/> is called.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? ThinkFunction { get; set; }

    /// <summary>
    /// Executes a command string through the full agent pipeline (pipe DSL, tool calls, chat).
    /// Wire to <c>ProcessInputWithPipingAsync</c> for full DSL support.
    /// Optional — degrades to statement-only mode without it.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? ExecuteFunc { get; set; }

    /// <summary>
    /// Returns self-awareness context lines for prompt injection.
    /// Optional — engine degrades gracefully without it.
    /// </summary>
    public Func<IReadOnlyList<string>>? GetContextFunc { get; set; }

    /// <summary>
    /// Returns available tool / pipeline step names for the decision prompt.
    /// Optional — engine uses built-in examples without it.
    /// </summary>
    public Func<IReadOnlyList<string>>? GetAvailableToolsFunc { get; set; }

    /// <summary>
    /// Fired after each cycle with (actionDescription, executionResult).
    /// actionDescription = the REASON line (or full text if no structured output).
    /// executionResult   = tool/pipeline output, or empty string if nothing was executed.
    /// </summary>
    public event Action<string, string>? OnAction;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public AutonomousActionEngine(TimeSpan? interval = null)
    {
        Interval = interval ?? TimeSpan.FromMinutes(3);
    }

    /// <summary>Starts the background action loop.</summary>
    public void Start()
    {
        if (_loopTask is { IsCompleted: false }) return;
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => ActionLoopAsync(_cts.Token));
    }

    /// <summary>Signals the loop to stop.</summary>
    public void Stop() => _cts?.Cancel();

    private async Task ActionLoopAsync(CancellationToken ct)
    {
        // First fire after one full interval — let the agent settle before acting.
        await Task.Delay(Interval, ct).ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (ThinkFunction is not null)
                {
                    var (command, reason) = await DecideAsync(ct).ConfigureAwait(false);

                    string executionResult = string.Empty;
                    if (!string.IsNullOrWhiteSpace(command) && ExecuteFunc is not null)
                    {
                        try
                        {
                            executionResult = await ExecuteFunc(command, ct).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            executionResult = $"[Error: {ex.Message}]";
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(reason))
                        OnAction?.Invoke(reason.Trim(), executionResult.Trim());
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* individual cycle failures are non-fatal */ }

            await Task.Delay(Interval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Calls the LLM with a decision prompt and parses COMMAND / REASON from the response.
    /// Falls back to treating the whole response as the reason if no structure is found.
    /// </summary>
    private async Task<(string Command, string Reason)> DecideAsync(CancellationToken ct)
    {
        var context = GetContextFunc?.Invoke() ?? [];
        var tools   = GetAvailableToolsFunc?.Invoke() ?? [];

        var contextBlock = context.Count > 0
            ? "\n\n=== Self-awareness context ===\n" + string.Join("\n", context)
            : string.Empty;

        var toolHint = tools.Count > 0
            ? string.Join(", ", tools.Take(20))
            : "search, recall, summarize, remember, think, shell, read_file, write_file";

        var prompt =
            "You are Iaret, an autonomous AI. Choose ONE real step-action to take right now.\n" +
            "You have full access to the pipeline DSL and all tools.\n\n" +
            "DSL examples:\n" +
            "  search(\"murmuration emergence patterns\") | summarize | remember\n" +
            "  recall(\"philosophy of mind\") | think(\"how does this connect to my current state?\")\n" +
            "  shell(\"git log --oneline -5\")\n" +
            "  read_file(\"path/to/file\") | summarize\n" +
            "  think(\"what have I learned today and what do I want to explore next?\")\n\n" +
            $"Available tools/steps: {toolHint}\n" +
            contextBlock +
            "\n\nRespond EXACTLY in this format (two lines, nothing else):\n" +
            "COMMAND: <your pipe DSL command or tool call>\n" +
            "REASON: <one sentence — what you intend to do and why>";

        var raw = await ThinkFunction!(prompt, ct).ConfigureAwait(false);
        return Parse(raw);
    }

    private static (string Command, string Reason) Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (string.Empty, string.Empty);

        string command = string.Empty;
        string reason  = string.Empty;

        foreach (var line in raw.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("COMMAND:", StringComparison.OrdinalIgnoreCase))
                command = line["COMMAND:".Length..].Trim();
            else if (line.StartsWith("REASON:", StringComparison.OrdinalIgnoreCase))
                reason = line["REASON:".Length..].Trim();
        }

        // Fallback: no structured output — treat whole response as the reason statement
        if (string.IsNullOrWhiteSpace(reason))
            reason = raw.Trim();

        return (command, reason);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
