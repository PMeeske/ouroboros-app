namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Prompt history buffer with Up/Down arrow navigation, inspired by Crush's REPL.
///
/// Up   → walk backward through history (oldest → newest displayed in reverse)
/// Down → walk forward; at the newest entry returns the saved draft
/// </summary>
public sealed class PromptHistory
{
    private readonly List<string> _entries = [];
    private int _cursor = -1;          // -1 means "not navigating"
    private string _draft = string.Empty;  // input saved when user starts navigating

    /// <summary>Number of entries retained.</summary>
    public int Count => _entries.Count;

    // ── Write ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves a submitted entry. Duplicate consecutive entries are deduplicated.
    /// Resets the navigation cursor so the next Up starts from the newest entry.
    /// </summary>
    public void Push(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        // Deduplicate consecutive identical entries
        if (_entries.Count == 0 || _entries[^1] != input)
            _entries.Add(input);

        _cursor = -1;
        _draft = string.Empty;
    }

    // ── Navigate ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the user presses Up. Returns the entry to display, or
    /// <c>null</c> if there is no history.
    /// </summary>
    /// <param name="currentInput">
    /// The text currently in the input box, used to save as draft on first navigation.
    /// </param>
    public string? NavigateUp(string currentInput)
    {
        if (_entries.Count == 0) return null;

        if (_cursor == -1)
            _draft = currentInput;  // save draft on first Up press

        _cursor = Math.Min(_cursor + 1, _entries.Count - 1);
        return _entries[^(_cursor + 1)];   // most recent first
    }

    /// <summary>
    /// Called when the user presses Down. Returns the next-newer entry, or
    /// the saved draft once they return past the newest entry.
    /// </summary>
    public string NavigateDown()
    {
        if (_cursor <= 0)
        {
            _cursor = -1;
            return _draft;
        }

        _cursor--;
        return _entries[^(_cursor + 1)];
    }

    /// <summary>Returns <c>true</c> when the user is actively navigating history.</summary>
    public bool IsNavigating => _cursor >= 0;

    /// <summary>Cancels navigation and restores the draft.</summary>
    public string CancelNavigation()
    {
        _cursor = -1;
        return _draft;
    }
}
