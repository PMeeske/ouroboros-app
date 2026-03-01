// <copyright file="Gateway.cs" company="OpenClaw.Sdk">
// .NET port of the OpenClaw Python SDK (github.com/masteryodaa/openclaw-sdk)
// </copyright>

using System.Text.Json;

namespace OpenClaw.Sdk.Gateway;

/// <summary>
/// Abstract base for all gateway implementations.
/// Two primitives: <see cref="CallAsync"/> and <see cref="SubscribeAsync"/>.
/// All higher-level methods are typed wrappers over those two.
/// </summary>
public abstract class GatewayBase : IAsyncDisposable
{
    // ── Connection lifecycle ────────────────────────────────────────────

    public abstract Task ConnectAsync(CancellationToken ct = default);
    public abstract Task CloseAsync(CancellationToken ct = default);
    public abstract Task<HealthStatus> HealthAsync(CancellationToken ct = default);

    // ── Protocol primitives ─────────────────────────────────────────────

    public abstract Task<JsonElement> CallAsync(
        string method,
        Dictionary<string, object?>? parameters = null,
        float? timeout = null,
        CancellationToken ct = default);

    public abstract IAsyncEnumerable<StreamEvent> SubscribeAsync(
        IReadOnlyList<string>? eventTypes = null,
        CancellationToken ct = default);

    // ── Chat facade ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ChatHistoryAsync(
        string sessionKey, int limit = 100, CancellationToken ct = default)
    {
        var result = await CallAsync("chat.history",
            new() { ["sessionKey"] = sessionKey, ["limit"] = (object?)limit }, ct: ct);
        return ParseArray(result, "messages");
    }

    public Task<JsonElement> ChatAbortAsync(string sessionKey, CancellationToken ct = default)
        => CallAsync("chat.abort", new() { ["sessionKey"] = sessionKey }, ct: ct);

    public Task<JsonElement> ChatInjectAsync(string sessionKey, string message, CancellationToken ct = default)
        => CallAsync("chat.inject", new() { ["sessionKey"] = sessionKey, ["message"] = message }, ct: ct);

    public Task<JsonElement> ChatSendAsync(Dictionary<string, object?> @params, CancellationToken ct = default)
        => CallAsync("chat.send", @params, ct: ct);

    // ── Sessions facade ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<Dictionary<string, object?>>> SessionsListAsync(CancellationToken ct = default)
    {
        var result = await CallAsync("sessions.list", new(), ct: ct);
        return ParseArray(result, "sessions");
    }

    public Task<JsonElement> SessionsResolveAsync(string key, CancellationToken ct = default)
        => CallAsync("sessions.resolve", new() { ["key"] = key }, ct: ct);

    public Task<JsonElement> SessionsResetAsync(string key, CancellationToken ct = default)
        => CallAsync("sessions.reset", new() { ["key"] = key }, ct: ct);

    public Task<JsonElement> SessionsDeleteAsync(string key, CancellationToken ct = default)
        => CallAsync("sessions.delete", new() { ["key"] = key }, ct: ct);

    public Task<JsonElement> SessionsCompactAsync(string key, CancellationToken ct = default)
        => CallAsync("sessions.compact", new() { ["key"] = key }, ct: ct);

    // ── Config facade ───────────────────────────────────────────────────

    public Task<JsonElement> ConfigGetAsync(CancellationToken ct = default)
        => CallAsync("config.get", new(), ct: ct);

    public Task<JsonElement> ConfigSetAsync(string raw, CancellationToken ct = default)
        => CallAsync("config.set", new() { ["raw"] = raw }, ct: ct);

    // ── Agent facade ────────────────────────────────────────────────────

    public Task<JsonElement> AgentWaitAsync(string runId, float? timeout = null, CancellationToken ct = default)
        => CallAsync("agent.wait", new() { ["runId"] = runId }, timeout, ct);

    // ── IAsyncDisposable ────────────────────────────────────────────────

    public async ValueTask DisposeAsync() => await CloseAsync();

    // ── Helpers ─────────────────────────────────────────────────────────

    private static IReadOnlyList<Dictionary<string, object?>> ParseArray(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];
        return arr.EnumerateArray()
            .Select(el => el.Deserialize<Dictionary<string, object?>>() ?? [])
            .ToList();
    }
}
