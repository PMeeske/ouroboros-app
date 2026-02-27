namespace Ouroboros.CLI.Infrastructure;

using System.Collections.Concurrent;
using Ouroboros.CLI.Avatar;
using Spectre.Console;

/// <summary>
/// Interactive tool-approval dialog inspired by Crush's three-button permission model.
///
/// Each tool call that requires approval blocks the calling task until the user
/// presses one of:
///   [a]  Allow once
///   [s]  Allow for this session (auto-approve identical future requests)
///   [d]  Deny  (also triggered by Esc)
///
/// Layer priority (same as Crush):
///   1. <see cref="SkipAll"/>  → always Allow
///   2. Session-level allowlist → Allow without prompting
///   3. Interactive prompt      → block until user responds
/// </summary>
public sealed class ToolPermissionBroker
{
    // Keys approved for the lifetime of this broker instance (= one session)
    private readonly HashSet<string> _sessionApprovals = [];
    private readonly object _consoleLock = new();

    /// <summary>
    /// When <c>true</c> all requests are auto-approved (equivalent to Crush's --yolo flag).
    /// </summary>
    public bool SkipAll { get; set; }

    // ── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Requests approval for a tool call. Blocks until the user responds or
    /// <paramref name="ct"/> is cancelled (which results in <see cref="PermissionAction.Deny"/>).
    /// </summary>
    public async Task<PermissionAction> RequestAsync(
        string toolName,
        string action,
        string? detail = null,
        CancellationToken ct = default)
    {
        if (SkipAll)
            return PermissionAction.Allow;

        var key = MakeKey(toolName, action);
        if (_sessionApprovals.Contains(key))
            return PermissionAction.Allow;

        // Run the blocking console prompt on a background thread so the
        // calling async context isn't blocked at the thread-pool level.
        try
        {
            return await Task.Run(() => PromptUser(toolName, action, detail, key), ct);
        }
        catch (OperationCanceledException)
        {
            return PermissionAction.Deny;
        }
    }

    // ── Interactive prompt ──────────────────────────────────────────────────────

    private PermissionAction PromptUser(string toolName, string action, string? detail, string key)
    {
        lock (_consoleLock)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(OuroborosTheme.Warn("  ⚡ Tool approval required"));

            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Tool:")}   {Markup.Escape(toolName)}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Action:")} {Markup.Escape(action)}");
            if (!string.IsNullOrEmpty(detail))
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Detail:")} {Markup.Escape(detail)}");

            AnsiConsole.Markup(OuroborosTheme.Dim("  [[a]] Allow  [[s]] Allow for session  [[d]] Deny  > "));

            while (true)
            {
                ConsoleKeyInfo keyInfo;
                try { keyInfo = Console.ReadKey(intercept: true); }
                catch (InvalidOperationException) { return PermissionAction.Deny; /* console not available */ }

                var ch = char.ToLowerInvariant(keyInfo.KeyChar);

                if (ch == 'a')
                {
                    AnsiConsole.MarkupLine(OuroborosTheme.Ok("allow"));
                    AnsiConsole.WriteLine();
                    return PermissionAction.Allow;
                }

                if (ch == 's')
                {
                    AnsiConsole.MarkupLine(OuroborosTheme.Ok("allow for session"));
                    AnsiConsole.WriteLine();
                    _sessionApprovals.Add(key);
                    return PermissionAction.Allow;
                }

                if (ch == 'd' || keyInfo.Key == ConsoleKey.Escape)
                {
                    AnsiConsole.MarkupLine("[red]deny[/]");
                    AnsiConsole.WriteLine();
                    return PermissionAction.Deny;
                }

                // Any other key: re-show hint
                Console.Write("\r");
                AnsiConsole.Markup(OuroborosTheme.Dim("  [[a]] Allow  [[s]] Allow for session  [[d]] Deny  > "));
            }
        }
    }

    private static string MakeKey(string toolName, string action) => $"{toolName}:{action}";
}

/// <summary>Result of a <see cref="ToolPermissionBroker"/> approval request.</summary>
public enum PermissionAction
{
    Allow,
    Deny,
}
