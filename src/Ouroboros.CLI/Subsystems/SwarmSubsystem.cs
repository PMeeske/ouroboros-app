// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Application.Swarm;

/// <summary>
/// Swarm subsystem: manages claude-flow MCP lifecycle, swarm operations,
/// and ethics-gated self-modification through swarm orchestration.
/// Connects lazily to avoid blocking agent startup (npx startup is slow).
/// </summary>
public sealed class SwarmSubsystem : ISwarmSubsystem, IAsyncDisposable
{
    public string Name => "Swarm";
    public bool IsInitialized { get; private set; }

    /// <summary>Whether the claude-flow MCP server is connected and responsive.</summary>
    public bool IsSwarmConnected => _service?.IsConnected ?? false;

    private ClaudeFlowMcpService? _service;
    private ClaudeFlowConfig _config = new();

    public Task InitializeAsync(SubsystemInitContext ctx)
    {
        // Create the MCP service but don't connect yet (lazy connect on first use).
        // The npx command takes several seconds to start, so we avoid blocking init.
        _service = new ClaudeFlowMcpService(_config);

        IsInitialized = true;
        ctx.Output.RecordInit("Swarm", true, "claude-flow ready (lazy connect)");
        return Task.CompletedTask;
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_service == null)
            throw new InvalidOperationException("Swarm subsystem not initialized");
        if (!_service.IsConnected)
            await _service.InitializeAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SWARM OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<SwarmInitResult> InitSwarmAsync(CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        return await _service!.InitSwarmAsync(_config.Topology, _config.MaxAgents, _config.Strategy, ct);
    }

    public async Task<SwarmStatusResult> GetStatusAsync(CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        return await _service!.GetSwarmStatusAsync(ct);
    }

    public async Task ShutdownSwarmAsync(CancellationToken ct = default)
    {
        if (_service?.IsConnected == true)
            await _service.ShutdownSwarmAsync(ct);
    }

    public async Task<SwarmHealthResult> GetSwarmHealthAsync(CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        return await _service!.GetSwarmHealthAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AGENT OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<AgentSpawnResult> SpawnAgentAsync(
        string type, string? name = null,
        IReadOnlyList<string>? capabilities = null,
        CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        return await _service!.SpawnAgentAsync(type, name, capabilities, ct);
    }

    public async Task<IReadOnlyList<AgentListEntry>> ListAgentsAsync(
        string filter = "all", CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        return await _service!.ListAgentsAsync(filter, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TASK ORCHESTRATION
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<TaskOrchestrationResult> OrchestrateAsync(
        string task, string strategy = "adaptive",
        int maxAgents = 5, string priority = "medium",
        CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        return await _service!.OrchestrateTaskAsync(task, strategy, maxAgents, priority, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DISPOSAL
    // ═══════════════════════════════════════════════════════════════════════════

    public async ValueTask DisposeAsync()
    {
        if (_service != null)
            await _service.DisposeAsync();
    }
}
