using System.Text;

namespace Ouroboros.Android.Services;

/// <summary>
/// CLI command executor — thin client that delegates AI operations to the Ouroboros WebAPI.
/// Local-only commands (help, history, shell, etc.) are handled directly on-device.
/// </summary>
public partial class CliExecutor
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

}
