using System.Text;

namespace Ouroboros.Android.Services;

/// <summary>
/// CLI command executor — thin client that delegates AI operations to the Ouroboros WebAPI.
/// Local-only commands (help, history, shell, etc.) are handled directly on-device.
/// </summary>
public class CliExecutor
{
    private readonly OuroborosApiClient _apiClient;
    private readonly CommandExecutor _commandExecutor;
    private readonly CommandHistoryService? _historyService;
    private readonly CommandSuggestionEngine? _suggestionEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliExecutor"/> class.
    /// </summary>
    /// <param name="databasePath">Optional path to SQLite database for history</param>
    /// <param name="apiEndpoint">Ouroboros WebAPI base URL (default: http://localhost:5000)</param>
    public CliExecutor(string? databasePath = null, string apiEndpoint = DefaultEndpoints.OuroborosApi)
    {
        _apiClient = new OuroborosApiClient(apiEndpoint);
        _commandExecutor = new CommandExecutor(requiresRoot: false);

        if (!string.IsNullOrEmpty(databasePath))
        {
            try
            {
                _historyService = new CommandHistoryService(databasePath);
                _suggestionEngine = new CommandSuggestionEngine(_historyService);
            }
            catch
            {
                _historyService = null;
                _suggestionEngine = null;
            }
        }
    }

    /// <summary>
    /// Gets or sets the Ouroboros WebAPI endpoint URL.
    /// </summary>
    public string ApiEndpoint
    {
        get => _apiClient.BaseUrl;
        set => _apiClient.BaseUrl = value;
    }

    /// <summary>
    /// Execute a CLI command and return the output.
    /// </summary>
    public async Task<string> ExecuteCommandAsync(string command)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command))
                return "Error: Empty command";

            if (_historyService != null)
                await _historyService.AddCommandAsync(command);

            var parts = ParseCommand(command);
            var cmd = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;

            return cmd switch
            {
                "help" => GetHelpText(),
                "version" => GetVersionInfo(),
                "about" => GetAboutInfo(),
                "ask" => await ExecuteAskAsync(parts),
                "pipeline" => await ExecutePipelineAsync(parts),
                "config" => ExecuteConfigCommand(parts),
                "status" => await GetStatusInfoAsync(),
                "ping" => await TestConnectionAsync(),
                "hints" => GetEfficiencyHints(),
                "suggest" => await GetSuggestionsAsync(parts),
                "history" => await GetHistoryAsync(parts),
                "shell" => await ExecuteShellAsync(parts),
                "clear" => "CLEAR_SCREEN",
                "exit" or "quit" => "Use the back button to exit the app",
                _ => await HandleUnknownCommandAsync(cmd)
            };
        }
        catch (InvalidOperationException ex)
        {
            return $"Error executing command: {ex.Message}";
        }
        catch (HttpRequestException ex)
        {
            return $"Error executing command: {ex.Message}";
        }
    }

    // ── Local commands ─────────────────────────────────────────────────

    private string GetHelpText()
    {
        return $@"Available Commands:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

System Commands:
  help         Show this help message
  version      Show version information
  about        About Ouroboros
  clear        Clear the screen
  exit/quit    Exit instructions

Connection:
  config       Configure WebAPI endpoint
               Usage: config <url>
               Example: config http://192.168.1.100:5000
  status       Show connection and service status
  ping         Test connection to the Ouroboros WebAPI

AI (via WebAPI):
  ask          Ask a question
               Usage: ask <your question>
               Example: ask What is functional programming?
  pipeline     Execute a DSL pipeline
               Usage: pipeline <dsl expression>
               Example: pipeline SetTopic('AI') | UseDraft

Intelligence & History:
  suggest      Get command suggestions
               Usage: suggest [partial command]
  history      Show recent command history
               Usage: history [count]
  hints        Tips for using the mobile CLI

Advanced:
  shell        Execute native shell command
               Usage: shell <command>

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Current WebAPI: {ApiEndpoint}
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
    }

    private string GetVersionInfo()
    {
        return @"Ouroboros CLI v2.0.0
Platform: Android (MAUI)
Architecture: Thin client → WebAPI
.NET 10.0";
    }

    private string GetAboutInfo()
    {
        return @"Ouroboros
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

A sophisticated functional programming-based
AI pipeline system.

This Android app is a thin client that connects
to the Ouroboros WebAPI for all AI operations.

Features:
• Ask questions via WebAPI
• Execute DSL pipelines
• Command history & suggestions
• Shell access

Developed by Adaptive Systems Inc.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

GitHub: PMeeske/Ouroboros
License: Open Source";
    }

    private string ExecuteConfigCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            return $@"Current WebAPI endpoint: {ApiEndpoint}

Usage: config <url>
Example: config http://192.168.1.100:5000

The endpoint should point to a running Ouroboros WebAPI instance.";
        }

        var newEndpoint = parts[1];
        if (!newEndpoint.StartsWith("http"))
        {
            return "Error: Endpoint must start with http:// or https://";
        }

        ApiEndpoint = newEndpoint;

        return $@"✓ Endpoint configured: {ApiEndpoint}

Test with: ping
Then try:  ask What is functional programming?";
    }

    private string GetEfficiencyHints()
    {
        return @"Tips for Mobile CLI:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Connection:
  • Ensure the Ouroboros WebAPI is running
  • Use WiFi for lowest latency
  • Test connection with 'ping' before queries

Usage:
  • Keep questions focused and specific
  • Use 'pipeline' for multi-step operations
  • Use 'suggest' for command autocompletion
  • Use 'history' to recall previous commands

Saving Battery:
  • Close app when done
  • Use device power saver for long sessions

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
    }

    // ── WebAPI-delegated commands ──────────────────────────────────────

    private async Task<string> ExecuteAskAsync(string[] parts)
    {
        if (parts.Length < 2)
            return "Usage: ask <your question>\nExample: ask What is functional programming?";

        var question = string.Join(" ", parts.Skip(1));

        try
        {
            var result = await _apiClient.AskAsync(question);

            var sb = new StringBuilder();
            sb.AppendLine($"Q: {question}");
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            if (result.Model != null)
                sb.AppendLine($"Model: {result.Model}");

            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine();
            sb.AppendLine($"A: {result.Answer}");

            if (result.ExecutionTimeMs.HasValue)
            {
                sb.AppendLine();
                sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine($"Completed in {result.ExecutionTimeMs}ms");
            }

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $@"Could not reach the Ouroboros WebAPI at {ApiEndpoint}

Error: {ex.Message}

Troubleshooting:
  1. Check connection: ping
  2. Verify endpoint: config
  3. Ensure WebAPI is running on the server";
        }
        catch (TaskCanceledException)
        {
            return "Request timed out. The server may be busy — try again.";
        }
        catch (InvalidOperationException ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private async Task<string> ExecutePipelineAsync(string[] parts)
    {
        if (parts.Length < 2)
            return "Usage: pipeline <dsl expression>\nExample: pipeline SetTopic('AI') | UseDraft | UseCritique";

        var dsl = string.Join(" ", parts.Skip(1));

        try
        {
            var result = await _apiClient.ExecutePipelineAsync(dsl);

            var sb = new StringBuilder();
            sb.AppendLine($"DSL: {dsl}");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine();
            sb.AppendLine(result);

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Could not reach WebAPI: {ex.Message}\n\nCheck connection with: ping";
        }
        catch (InvalidOperationException ex)
        {
            return $"Pipeline error: {ex.Message}";
        }
    }

    private async Task<string> GetStatusInfoAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("System Status:");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"WebAPI Endpoint: {ApiEndpoint}");

        var healthy = await _apiClient.IsHealthyAsync();
        sb.AppendLine($"Connection:      {(healthy ? "✓ Connected" : "✗ Unreachable")}");

        if (healthy)
        {
            try
            {
                var info = await _apiClient.GetServiceInfoAsync();
                sb.AppendLine();
                sb.AppendLine("Service Info:");
                sb.AppendLine(info);
            }
            catch
            {
                sb.AppendLine("Service Info:    Could not retrieve");
            }
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("To get started:");
            sb.AppendLine("  1. Start the Ouroboros WebAPI on your server");
            sb.AppendLine("  2. Configure endpoint: config <url>");
            sb.AppendLine("  3. Test: ping");
        }

        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        return sb.ToString();
    }

    private async Task<string> TestConnectionAsync()
    {
        try
        {
            var healthy = await _apiClient.IsHealthyAsync();

            return healthy
                ? $"✓ Connected to Ouroboros WebAPI at {ApiEndpoint}"
                : $"✗ Cannot reach WebAPI at {ApiEndpoint}\n\nCheck endpoint with: config";
        }
        catch (HttpRequestException ex)
        {
            return $"✗ Connection test failed: {ex.Message}";
        }
        catch (TaskCanceledException ex)
        {
            return $"✗ Connection test timed out: {ex.Message}";
        }
    }

    // ── Local-only commands ────────────────────────────────────────────

    private async Task<string> GetSuggestionsAsync(string[] parts)
    {
        if (_suggestionEngine == null)
            return "Suggestion engine not available (database not initialized).";

        var partialCommand = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;

        try
        {
            var suggestions = await _suggestionEngine.GetSuggestionsAsync(partialCommand, 10);

            if (suggestions.Count == 0)
                return "No suggestions found. Type 'help' to see available commands.";

            var sb = new StringBuilder();
            sb.AppendLine("Command Suggestions:");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var suggestion in suggestions)
            {
                sb.AppendLine($"• {suggestion.Command}");
                if (!string.IsNullOrEmpty(suggestion.Description))
                    sb.AppendLine($"  {suggestion.Description}");
                sb.AppendLine($"  Confidence: {suggestion.Confidence:P0} | Source: {suggestion.Source}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (InvalidOperationException ex)
        {
            return $"Error getting suggestions: {ex.Message}";
        }
    }

    private async Task<string> GetHistoryAsync(string[] parts)
    {
        if (_historyService == null)
            return "Command history not available (database not initialized).";

        var count = 20;
        if (parts.Length > 1 && int.TryParse(parts[1], out var requestedCount))
            count = Math.Min(requestedCount, 100);

        try
        {
            var history = await _historyService.GetRecentHistoryAsync(count);

            if (history.Count == 0)
                return "No command history yet.";

            var sb = new StringBuilder();
            sb.AppendLine($"Recent Command History (last {history.Count}):");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var entry in history)
                sb.AppendLine($"[{entry.ExecutedAt:yyyy-MM-dd HH:mm:ss}] {entry.Command}");

            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("\nUse 'suggest' to get command suggestions based on history.");

            return sb.ToString();
        }
        catch (InvalidOperationException ex)
        {
            return $"Error retrieving history: {ex.Message}";
        }
    }

    private async Task<string> ExecuteShellAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            return @"Usage: shell <command>
Examples:
  shell ls -la
  shell ps aux
  shell df -h

⚠️  Shell commands run with app permissions only.";
        }

        var shellCommand = string.Join(" ", parts.Skip(1));
        var validation = _commandExecutor.ValidateCommand(shellCommand);
        if (!validation.IsValid)
            return $"⚠️  Command validation failed: {validation.Message}";

        try
        {
            var result = await _commandExecutor.ExecuteAsync(shellCommand);

            var sb = new StringBuilder();
            sb.AppendLine($"$ {shellCommand}");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            if (!string.IsNullOrEmpty(result.Output))
                sb.AppendLine(result.Output);
            if (!string.IsNullOrEmpty(result.Error))
            {
                sb.AppendLine("STDERR:");
                sb.AppendLine(result.Error);
            }

            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Exit Code: {result.ExitCode}");

            return sb.ToString();
        }
        catch (InvalidOperationException ex)
        {
            return $"Error executing shell command: {ex.Message}";
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return $"Error executing shell command: {ex.Message}";
        }
    }

    private async Task<string> HandleUnknownCommandAsync(string cmd)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Unknown command: {cmd}");
        sb.AppendLine();

        if (_suggestionEngine != null)
        {
            try
            {
                var suggestions = await _suggestionEngine.GetSuggestionsAsync(cmd, 3);
                if (suggestions.Count > 0)
                {
                    sb.AppendLine("Did you mean:");
                    foreach (var suggestion in suggestions)
                        sb.AppendLine($"  • {suggestion.Command}");
                    sb.AppendLine();
                }
            }
            catch
            {
                // Ignore suggestion errors
            }
        }

        sb.AppendLine("Type 'help' for available commands");
        return sb.ToString();
    }

    // ── Parsing ────────────────────────────────────────────────────────

    private static string[] ParseCommand(string command)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        foreach (var c in command)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        return parts.ToArray();
    }
}
