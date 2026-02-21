namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Extensible slash-command registry inspired by Crush's command palette.
///
/// Commands can be registered by subsystems at startup and are resolved
/// when the user types a / prefix in the REPL. The registry supports:
///   • Exact name lookup  (e.g. "/exit")
///   • Fuzzy prefix filter for an interactive picker
///   • Optional keyboard shortcut hint displayed in help tables
///
/// Registration example:
/// <code>
/// registry
///   .Register(new SlashCommand("clear",   "Clear the screen",    Shortcut: "ctrl+l"))
///   .Register(new SlashCommand("session", "List past sessions",  Shortcut: "ctrl+s",
///       Execute: async (args, ct) => { ... return SlashCommandResult.Handled(); }));
/// </code>
/// </summary>
public sealed class SlashCommandRegistry
{
    private readonly List<SlashCommand> _commands = [];

    // ── Registration ────────────────────────────────────────────────────────────

    public SlashCommandRegistry Register(SlashCommand command)
    {
        _commands.Add(command);
        return this;
    }

    public SlashCommandRegistry Register(
        string name,
        string description,
        string? shortcut = null,
        Func<string[], CancellationToken, Task<SlashCommandResult>>? execute = null)
        => Register(new SlashCommand(name, description, shortcut, execute));

    // ── Lookup ──────────────────────────────────────────────────────────────────

    /// <summary>All registered commands in registration order.</summary>
    public IReadOnlyList<SlashCommand> All => _commands;

    /// <summary>Finds an exact name match (case-insensitive).</summary>
    public SlashCommand? Find(string name) =>
        _commands.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Fuzzy filter: keeps commands whose name or description contains
    /// <paramref name="query"/> (case-insensitive). Returns all commands if query is empty.
    /// </summary>
    public IReadOnlyList<SlashCommand> Filter(string query)
    {
        if (string.IsNullOrEmpty(query)) return _commands;

        return _commands
            .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || c.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // ── Dispatch ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <paramref name="input"/> (e.g. "/model gpt-4o") and dispatches to
    /// the matching command. Returns <c>null</c> if no command matched.
    /// </summary>
    public async Task<SlashCommandResult?> DispatchAsync(string input, CancellationToken ct = default)
    {
        if (!input.StartsWith('/')) return null;

        var parts = input[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        var cmd = Find(parts[0]);
        if (cmd is null) return null;

        if (cmd.Execute is null)
            return SlashCommandResult.Unhandled($"/{cmd.Name} has no handler");

        var args = parts[1..];
        return await cmd.Execute(args, ct);
    }
}

// ── Value types ─────────────────────────────────────────────────────────────────

/// <summary>
/// A single slash command entry.
/// </summary>
/// <param name="Name">Command keyword without the leading slash.</param>
/// <param name="Description">Short description shown in help and the palette.</param>
/// <param name="Shortcut">Optional keybinding hint (display only, e.g. "ctrl+n").</param>
/// <param name="Execute">
/// Optional async handler. Receives the remaining tokens after the command name
/// and a cancellation token. Return <see cref="SlashCommandResult.Handled"/> or
/// <see cref="SlashCommandResult.Exit"/>.
/// </param>
public sealed record SlashCommand(
    string Name,
    string Description,
    string? Shortcut = null,
    Func<string[], CancellationToken, Task<SlashCommandResult>>? Execute = null);

/// <summary>Result returned by a slash command handler.</summary>
public sealed record SlashCommandResult(
    bool WasHandled,
    string? Output = null,
    bool ShouldExit = false)
{
    public static SlashCommandResult Handled(string? output = null) =>
        new(true, output);

    public static SlashCommandResult Exit(string? output = null) =>
        new(true, output, ShouldExit: true);

    public static SlashCommandResult Unhandled(string? reason = null) =>
        new(false, reason);
}
