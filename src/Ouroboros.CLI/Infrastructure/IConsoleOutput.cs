using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Centralized console output service with verbosity-aware routing.
/// All terminal output should flow through this service rather than
/// using Console.Write* directly.
/// </summary>
public interface IConsoleOutput
{
    OutputVerbosity Verbosity { get; }

    // ── Init-phase output ──────────────────────────────────────

    /// <summary>
    /// Records a subsystem init result. In Normal mode, only failures
    /// are displayed. In Verbose mode, all lines are shown.
    /// </summary>
    void RecordInit(string subsystemName, bool success, string? detail = null);

    /// <summary>
    /// Prints the collapsed init summary or full output depending on verbosity.
    /// </summary>
    void FlushInitSummary();

    // ── Conversation output ────────────────────────────────────

    void WriteResponse(string personaName, string text);
    void WriteSystem(string text);

    // ── Debug / diagnostic output ──────────────────────────────

    void WriteDebug(string text);
    void WriteWarning(string text);
    void WriteError(string text);

    // ── Spinner ────────────────────────────────────────────────

    /// <summary>
    /// Shows a single-line spinner that overwrites itself in-place.
    /// Returns a handle; disposing it erases the spinner line.
    /// </summary>
    ISpinnerHandle StartSpinner(string label);

    // ── Tool display (Crush-style) ─────────────────────────────

    /// <summary>
    /// Prints a pending tool header: <c>  ● ToolName  param</c>
    /// </summary>
    void WriteToolCall(string toolName, string? param = null);

    /// <summary>
    /// Prints a completed tool line (✓/✗) followed by a truncated result body.
    /// </summary>
    void WriteToolResult(string toolName, bool success, string? output = null, int maxLines = 10);

    // ── Status bar ─────────────────────────────────────────────

    /// <summary>
    /// Writes a compact status line modelled on Crush's header:
    /// <c>  model · dir  contextPct%</c>
    /// Only shown in Normal/Verbose verbosity.
    /// </summary>
    void WriteStatusBar(string model, string? workingDir = null, int? contextPct = null);

    // ── Welcome / Banner ───────────────────────────────────────

    void WriteWelcome(string personaName, string model, string? mood = null);
}