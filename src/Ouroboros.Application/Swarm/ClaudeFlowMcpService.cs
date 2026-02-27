// <copyright file="ClaudeFlowMcpService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Mcp;

namespace Ouroboros.Application.Swarm;

/// <summary>
/// Typed wrapper over <see cref="McpClient"/> for the claude-flow MCP server.
/// Provides async methods for swarm orchestration, agent management, memory,
/// task orchestration, and neural operations.
/// </summary>
public sealed class ClaudeFlowMcpService : IAsyncDisposable
{
    private readonly McpClient _client;
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeFlowMcpService"/> class.
    /// </summary>
    public ClaudeFlowMcpService(string command, params string[] args)
    {
        _client = new McpClient(command, args);
    }

    /// <summary>
    /// Initializes from a <see cref="ClaudeFlowConfig"/>.
    /// </summary>
    public ClaudeFlowMcpService(ClaudeFlowConfig config)
        : this(config.Command, config.Args)
    {
    }

    /// <summary>Whether the MCP server process is connected.</summary>
    public bool IsConnected => _client.IsConnected;

    /// <summary>
    /// Connects to the claude-flow MCP server and performs the initialization handshake.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized && _client.IsConnected) return;
        await _client.ConnectAsync(ct);
        _initialized = true;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SWARM OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Initializes a new swarm with the specified topology.</summary>
    public async Task<SwarmInitResult> InitSwarmAsync(
        string topology = "hierarchical-mesh",
        int maxAgents = 15,
        string strategy = "specialized",
        CancellationToken ct = default)
    {
        var result = await CallToolAsync("swarm_init", new()
        {
            ["topology"] = topology,
            ["maxAgents"] = maxAgents,
            ["strategy"] = strategy,
        }, ct);

        if (result.IsError)
            return new SwarmInitResult(false, "", topology, maxAgents, result.Content);

        return new SwarmInitResult(true, ExtractField(result.Content, "id", "swarm-1"),
            topology, maxAgents, result.Content);
    }

    /// <summary>Gets the current swarm status.</summary>
    public async Task<SwarmStatusResult> GetSwarmStatusAsync(CancellationToken ct = default)
    {
        var result = await CallToolAsync("swarm_status", new() { ["verbose"] = true }, ct);
        if (result.IsError)
            return new SwarmStatusResult(false, "", 0, "", result.Content);

        return new SwarmStatusResult(true,
            ExtractField(result.Content, "id", "unknown"),
            ExtractInt(result.Content, "agents", 0),
            ExtractField(result.Content, "topology", "unknown"),
            result.Content);
    }

    /// <summary>Shuts down the swarm gracefully.</summary>
    public async Task<string> ShutdownSwarmAsync(CancellationToken ct = default)
    {
        var result = await CallToolAsync("swarm_shutdown", null, ct);
        return result.Content;
    }

    /// <summary>Gets swarm health status.</summary>
    public async Task<SwarmHealthResult> GetSwarmHealthAsync(CancellationToken ct = default)
    {
        var result = await CallToolAsync("swarm_health", null, ct);
        if (result.IsError)
            return new SwarmHealthResult(false, "error", result.Content);

        return new SwarmHealthResult(true, "healthy", result.Content);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AGENT OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Spawns a new agent in the swarm.</summary>
    public async Task<AgentSpawnResult> SpawnAgentAsync(
        string type,
        string? name = null,
        IReadOnlyList<string>? capabilities = null,
        CancellationToken ct = default)
    {
        var args = new Dictionary<string, object?> { ["type"] = type };
        if (name != null) args["name"] = name;
        if (capabilities is { Count: > 0 }) args["capabilities"] = capabilities;

        var result = await CallToolAsync("agent_spawn", args, ct);
        if (result.IsError)
            return new AgentSpawnResult(false, "", type, name, result.Content);

        return new AgentSpawnResult(true,
            ExtractField(result.Content, "id", "agent-1"),
            type, name, result.Content);
    }

    /// <summary>Terminates an agent by ID.</summary>
    public async Task<string> TerminateAgentAsync(string agentId, CancellationToken ct = default)
    {
        var result = await CallToolAsync("agent_terminate", new() { ["agentId"] = agentId }, ct);
        return result.Content;
    }

    /// <summary>Gets the status of a specific agent.</summary>
    public async Task<AgentStatusResult> GetAgentStatusAsync(
        string agentId, CancellationToken ct = default)
    {
        var result = await CallToolAsync("agent_status", new() { ["agentId"] = agentId }, ct);
        return new AgentStatusResult(agentId,
            result.IsError ? "error" : "active",
            ExtractField(result.Content, "type", "unknown"),
            result.Content);
    }

    /// <summary>Lists all agents in the swarm.</summary>
    public async Task<IReadOnlyList<AgentListEntry>> ListAgentsAsync(
        string filter = "all", CancellationToken ct = default)
    {
        var result = await CallToolAsync("agent_list", new() { ["filter"] = filter }, ct);
        if (result.IsError) return [];

        return ParseAgentList(result.Content);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TASK ORCHESTRATION
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Orchestrates a task across swarm agents.</summary>
    public async Task<TaskOrchestrationResult> OrchestrateTaskAsync(
        string task,
        string strategy = "adaptive",
        int maxAgents = 5,
        string priority = "medium",
        CancellationToken ct = default)
    {
        var result = await CallToolAsync("task_orchestrate", new()
        {
            ["task"] = task,
            ["strategy"] = strategy,
            ["maxAgents"] = maxAgents,
            ["priority"] = priority,
        }, ct);

        if (result.IsError)
            return new TaskOrchestrationResult(false, "", "failed", result.Content);

        return new TaskOrchestrationResult(true,
            ExtractField(result.Content, "taskId", "task-1"),
            "completed", result.Content);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MEMORY OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Stores a value in swarm memory.</summary>
    public async Task<string> StoreMemoryAsync(
        string key, string value, string? ns = null, CancellationToken ct = default)
    {
        var args = new Dictionary<string, object?> { ["key"] = key, ["value"] = value };
        if (ns != null) args["namespace"] = ns;
        var result = await CallToolAsync("memory_store", args, ct);
        return result.Content;
    }

    /// <summary>Retrieves a value from swarm memory.</summary>
    public async Task<string> RetrieveMemoryAsync(
        string key, string? ns = null, CancellationToken ct = default)
    {
        var args = new Dictionary<string, object?> { ["key"] = key };
        if (ns != null) args["namespace"] = ns;
        var result = await CallToolAsync("memory_retrieve", args, ct);
        return result.Content;
    }

    /// <summary>Searches swarm memory using semantic/vector search.</summary>
    public async Task<IReadOnlyList<MemorySearchResult>> SearchMemoryAsync(
        string query, string? ns = null, int limit = 10, CancellationToken ct = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["limit"] = limit,
        };
        if (ns != null) args["namespace"] = ns;

        var result = await CallToolAsync("memory_search", args, ct);
        if (result.IsError) return [];

        return ParseMemorySearchResults(result.Content);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // NEURAL OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Triggers neural training.</summary>
    public async Task<string> NeuralTrainAsync(
        string? agentId = null, int iterations = 10, CancellationToken ct = default)
    {
        var args = new Dictionary<string, object?> { ["iterations"] = iterations };
        if (agentId != null) args["agentId"] = agentId;
        var result = await CallToolAsync("neural_train", args, ct);
        return result.Content;
    }

    /// <summary>Makes a neural prediction.</summary>
    public async Task<string> NeuralPredictAsync(string input, CancellationToken ct = default)
    {
        var result = await CallToolAsync("neural_predict", new() { ["input"] = input }, ct);
        return result.Content;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INTERNAL HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private Task<McpToolResult> CallToolAsync(
        string toolName, Dictionary<string, object?>? arguments, CancellationToken ct)
    {
        return _client.CallToolAsync(toolName, arguments, ct);
    }

    private static string ExtractField(string content, string fieldName, string fallback)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty(fieldName, out var val))
                return val.GetString() ?? fallback;
        }
        catch (System.Text.Json.JsonException)
        {
            // Content might not be JSON — try regex fallback
            var match = System.Text.RegularExpressions.Regex.Match(
                content, $@"""{fieldName}""\s*:\s*""([^""]+)"""); // dynamic field name — cannot use GeneratedRegex
            if (match.Success) return match.Groups[1].Value;
        }

        return fallback;
    }

    private static int ExtractInt(string content, string fieldName, int fallback)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty(fieldName, out var val))
                return val.GetInt32();
        }
        catch (System.Text.Json.JsonException) { /* non-JSON */ }

        return fallback;
    }

    private static IReadOnlyList<AgentListEntry> ParseAgentList(string content)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            var list = new List<AgentListEntry>();
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    list.Add(new AgentListEntry(
                        el.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        el.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "",
                        el.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "",
                        el.TryGetProperty("name", out var n) ? n.GetString() : null));
                }
            }

            return list;
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<MemorySearchResult> ParseMemorySearchResults(string content)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            var list = new List<MemorySearchResult>();
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    list.Add(new MemorySearchResult(
                        el.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "",
                        el.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "",
                        el.TryGetProperty("score", out var s) ? s.GetDouble() : 0));
                }
            }

            return list;
        }
        catch
        {
            return [];
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
