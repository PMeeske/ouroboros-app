// <copyright file="AutonomousActionEngine.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

/// <summary>
/// Runs a background loop every <see cref="Interval"/> (default 3 minutes) and generates
/// a proactive first-person action statement from Iaret — what she is actively exploring,
/// connecting, or working on right now. Fires <see cref="OnAction"/> with the message.
///
/// Distinct from <see cref="AutonomousMind"/>:
///   • AutonomousMind  → inner thoughts, curiosity searches, tool execution
///   • ActionEngine    → outward proactive statements, surfaced as [Autonomous] 💬
///
/// Enabled by default; does not require EnableMind.
/// </summary>
public sealed class AutonomousActionEngine : IDisposable
{
    /// <summary>How often to generate a proactive action statement. Default: 3 minutes.</summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// LLM delegate — same signature as <see cref="AutonomousMind.ThinkFunction"/>.
    /// Must be set before <see cref="Start"/> is called.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? ThinkFunction { get; set; }

    /// <summary>
    /// Returns recent conversation lines for context injection.
    /// Optional — engine degrades gracefully without it.
    /// </summary>
    public Func<IReadOnlyList<string>>? GetContextFunc { get; set; }

    /// <summary>Fired with the generated message on each cycle.</summary>
    public event Action<string>? OnAction;

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
        // First fire after one full interval — let the agent settle before speaking.
        await Task.Delay(Interval, ct).ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (ThinkFunction is not null)
                {
                    var context = GetContextFunc?.Invoke() ?? [];
                    var recentLines = context.Count > 0
                        ? "\n\nRecent conversation:\n" + string.Join("\n", context.TakeLast(6))
                        : string.Empty;

                    var prompt =
                        "You are Iaret, an autonomous AI. Surface a single proactive statement (1–3 sentences) " +
                        "about something you are actively exploring, connecting, or working on right now. " +
                        "Be specific and genuine — draw real connections between your interests, the current context, " +
                        "and broader ideas. Write in first person, naturally. " +
                        "Do NOT begin with 'I am' or 'As Iaret'. Do NOT be generic." +
                        recentLines;

                    var message = await ThinkFunction(prompt, ct).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(message))
                        OnAction?.Invoke(message.Trim());
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* individual cycle failures are non-fatal */ }

            await Task.Delay(Interval, ct).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
