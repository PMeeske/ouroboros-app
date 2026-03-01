// <copyright file="OpenClawClient.cs" company="OpenClaw.Sdk">
// .NET port of the OpenClaw Python SDK (github.com/masteryodaa/openclaw-sdk)
// </copyright>

using System.Net.Sockets;
using System.Text.Json;
using OpenClaw.Sdk.Config;
using OpenClaw.Sdk.Gateway;

namespace OpenClaw.Sdk;

/// <summary>
/// Top-level client for the OpenClaw SDK.
///
/// Create via the <see cref="ConnectAsync"/> factory::
///
///   var client = await OpenClawClient.ConnectAsync();
///   var agent  = client.GetAgent("my-agent");
///   var result = await agent.ExecuteAsync("Summarise the report");
///
/// Or as an <c>await using</c> block::
///
///   await using var client = await OpenClawClient.ConnectAsync();
///   var result = await client.GetAgent("bot").ExecuteAsync("hello");
/// </summary>
public sealed class OpenClawClient : IAsyncDisposable
{
    private readonly ClientConfig _config;
    private readonly GatewayBase _gateway;

    private OpenClawClient(ClientConfig config, GatewayBase gateway)
    {
        _config = config;
        _gateway = gateway;
    }

    // ── Factory ────────────────────────────────────────────────────────────

    /// <summary>
    /// Auto-detect and connect to an OpenClaw gateway.
    ///
    /// Detection order:
    ///   1. <see cref="ClientConfig.GatewayWsUrl"/> → <see cref="ProtocolGateway"/>
    ///   2. <c>OPENCLAW_GATEWAY_URL</c> env var     → <see cref="ProtocolGateway"/>
    ///   3. Local gateway at ws://127.0.0.1:18789   → <see cref="ProtocolGateway"/>
    ///   4. Throws <see cref="ConfigurationException"/>.
    /// </summary>
    public static async Task<OpenClawClient> ConnectAsync(
        ClientConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= ClientConfig.FromEnv();
        var gateway = BuildGateway(config);
        await gateway.ConnectAsync(ct);
        return new OpenClawClient(config, gateway);
    }

    private static GatewayBase BuildGateway(ClientConfig config)
    {
        // Explicit WS URL
        if (!string.IsNullOrEmpty(config.GatewayWsUrl))
            return new ProtocolGateway(config.GatewayWsUrl!, config.ApiKey);

        // Env var
        var envUrl = Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY_URL")
                  ?? Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY_WS_URL");
        if (!string.IsNullOrEmpty(envUrl))
            return new ProtocolGateway(envUrl!, config.ApiKey);

        // Local auto-detect
        if (config.Mode == GatewayMode.Local
            || (config.Mode == GatewayMode.Auto && IsLocalGatewayRunning()))
        {
            var token = config.ApiKey ?? ProtocolGateway.LoadToken();
            return new ProtocolGateway("ws://127.0.0.1:18789/gateway", token);
        }

        throw new ConfigurationException(
            "No OpenClaw gateway found. " +
            "Run 'openclaw gateway start', set GatewayWsUrl, or set OPENCLAW_GATEWAY_URL.");
    }

    // ── Agent access ───────────────────────────────────────────────────────

    /// <summary>
    /// Get an <see cref="Agent"/> by ID (no network call — lazy handle).
    /// </summary>
    public Agent GetAgent(string agentId, string sessionName = "main")
        => new(this, _gateway, agentId, sessionName);

    // ── Agents management ──────────────────────────────────────────────────

    /// <summary>List all known agents.</summary>
    public async Task<IReadOnlyList<AgentSummary>> ListAgentsAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _gateway.CallAsync("agents.list", null, ct: ct);
            if (!result.TryGetProperty("agents", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return [];

            return arr.EnumerateArray().Select(el =>
            {
                var id = el.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                return new AgentSummary(id, null, AgentStatus.Idle);
            }).ToList();
        }
        catch (GatewayException) { return []; }
    }

    // ── Sessions ───────────────────────────────────────────────────────────

    public Task<IReadOnlyList<Dictionary<string, object?>>> ListSessionsAsync(CancellationToken ct = default)
        => _gateway.SessionsListAsync(ct);

    // ── Health ─────────────────────────────────────────────────────────────

    public Task<HealthStatus> HealthAsync(CancellationToken ct = default)
        => _gateway.HealthAsync(ct);

    // ── Low-level access ───────────────────────────────────────────────────

    /// <summary>The underlying gateway for advanced/direct calls.</summary>
    public GatewayBase Gateway => _gateway;

    /// <summary>Current configuration.</summary>
    public ClientConfig Config => _config;

    // ── IAsyncDisposable ───────────────────────────────────────────────────

    public async ValueTask DisposeAsync() => await _gateway.DisposeAsync();

    // ── Helpers ────────────────────────────────────────────────────────────

    private static bool IsLocalGatewayRunning()
    {
        try
        {
            using var tcp = new TcpClient();
            return tcp.ConnectAsync("127.0.0.1", 18789).Wait(500);
        }
        catch { return false; }
    }
}
