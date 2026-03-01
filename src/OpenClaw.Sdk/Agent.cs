// <copyright file="Agent.cs" company="OpenClaw.Sdk">
// .NET port of the OpenClaw Python SDK (github.com/masteryodaa/openclaw-sdk)
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenClaw.Sdk.Config;
using OpenClaw.Sdk.Gateway;

namespace OpenClaw.Sdk;

/// <summary>
/// Represents a single OpenClaw agent.
///
/// Obtain via <see cref="OpenClawClient.GetAgent"/>::
///   var agent = client.GetAgent("my-agent");
///   var result = await agent.ExecuteAsync("Summarise the report");
///
/// The session key sent to the gateway is <c>"agent:{agentId}:{sessionName}"</c>.
/// </summary>
public sealed class Agent
{
    private readonly OpenClawClient _client;
    private readonly GatewayBase _gateway;

    public string AgentId { get; }
    public string SessionName { get; }

    /// <summary>Gateway session key, e.g. <c>"agent:main:main"</c>.</summary>
    public string SessionKey => $"agent:{AgentId}:{SessionName}";

    internal Agent(OpenClawClient client, GatewayBase gateway, string agentId, string sessionName = "main")
    {
        _client = client;
        _gateway = gateway;
        AgentId = agentId;
        SessionName = sessionName;
    }

    // ── Status ────────────────────────────────────────────────────────────

    public async Task<AgentStatus> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _gateway.SessionsResolveAsync(SessionKey, ct);
            var statusStr = result.TryGetProperty("status", out var s) ? s.GetString() : null;
            return statusStr?.ToLowerInvariant() switch
            {
                "running" => AgentStatus.Running,
                "idle"    => AgentStatus.Idle,
                "error"   => AgentStatus.Error,
                _         => AgentStatus.Idle,
            };
        }
        catch (GatewayException) { return AgentStatus.Idle; }
    }

    // ── Execution ──────────────────────────────────────────────────────────

    /// <summary>
    /// Send <paramref name="query"/> to the agent and return the final result.
    /// Subscribes to push events, sends chat.send, waits for DONE or ERROR.
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(
        string query,
        ExecutionOptions? options = null,
        CancellationToken ct = default)
    {
        var startMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var runId = (string?)null;

        // Subscribe to streaming events *before* sending the request
        await using var sub = new EventSubscription(_gateway, ct);

        var @params = BuildSendParams(query, options);
        var sendResult = await _gateway.ChatSendAsync(@params, ct);

        if (sendResult.TryGetProperty("runId", out var rid))
            runId = rid.GetString();

        // Collect events until DONE or ERROR
        var content = new System.Text.StringBuilder();
        var toolCalls = new List<ToolCallRecord>();
        var files = new List<GeneratedFile>();
        TokenUsage? tokenUsage = null;
        string? thinking = null;
        string? stopReason = null;
        string? errorMessage = null;

        await foreach (var ev in sub.WithCancellation(ct))
        {
            var payload = ev.Data;

            // Only process events matching our runId (if known)
            if (runId != null && payload.TryGetValue("runId", out var evRunId)
                && evRunId?.ToString() != runId)
                continue;

            switch (ev.EventType)
            {
                case EventType.Content:
                    if (payload.TryGetValue("content", out var c))
                        content.Append(c?.ToString());
                    break;

                case EventType.Thinking:
                    if (payload.TryGetValue("thinking", out var th))
                        thinking = th?.ToString();
                    break;

                case EventType.ToolCall:
                    var tool = payload.TryGetValue("tool", out var t) ? t?.ToString() ?? "" : "";
                    var inp = payload.TryGetValue("input", out var inp_) ? inp_?.ToString() ?? "" : "";
                    toolCalls.Add(new ToolCallRecord(tool, inp, null, null));
                    break;

                case EventType.Done:
                    if (payload.TryGetValue("content", out var dc))
                        content.Append(dc?.ToString());
                    if (payload.TryGetValue("stopReason", out var sr))
                        stopReason = sr?.ToString();
                    goto done;

                case EventType.Error:
                    errorMessage = payload.TryGetValue("message", out var em) ? em?.ToString() : "Agent error";
                    goto done;
            }
        }

        done:
        var latency = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startMs);
        var text = content.ToString();

        return new ExecutionResult(
            Success: errorMessage == null,
            Content: text,
            ContentBlocks: [],
            Files: files,
            ToolCalls: toolCalls,
            Thinking: thinking,
            LatencyMs: latency,
            TokenUsage: tokenUsage ?? new TokenUsage(),
            CompletedAt: DateTimeOffset.UtcNow,
            StopReason: stopReason ?? (errorMessage != null ? "error" : "complete"),
            ErrorMessage: errorMessage);
    }

    /// <summary>
    /// Send <paramref name="query"/> and stream back events as they arrive.
    /// </summary>
    public async IAsyncEnumerable<StreamEvent> ExecuteStreamAsync(
        string query,
        ExecutionOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var sub = new EventSubscription(_gateway, ct);

        var @params = BuildSendParams(query, options);
        await _gateway.ChatSendAsync(@params, ct);

        await foreach (var ev in sub.WithCancellation(ct))
        {
            yield return ev;
            if (ev.EventType is EventType.Done or EventType.Error)
                yield break;
        }
    }

    // ── History / session ops ──────────────────────────────────────────────

    public Task<IReadOnlyList<Dictionary<string, object?>>> GetHistoryAsync(int limit = 100, CancellationToken ct = default)
        => _gateway.ChatHistoryAsync(SessionKey, limit, ct);

    public Task ResetAsync(CancellationToken ct = default)
        => _gateway.SessionsResetAsync(SessionKey, ct);

    public Task CompactAsync(CancellationToken ct = default)
        => _gateway.SessionsCompactAsync(SessionKey, ct);

    public Task AbortAsync(CancellationToken ct = default)
        => _gateway.ChatAbortAsync(SessionKey, ct);

    // ── Helpers ───────────────────────────────────────────────────────────

    private Dictionary<string, object?> BuildSendParams(string query, ExecutionOptions? opts)
    {
        var p = new Dictionary<string, object?>
        {
            ["sessionKey"] = (object?)SessionKey,
            ["message"] = query,
            ["idempotencyKey"] = Guid.NewGuid().ToString("N"),
        };

        if (opts != null)
        {
            p["timeoutMs"] = opts.TimeoutSeconds * 1000;
            if (!string.IsNullOrEmpty(opts.Thinking))
                p["thinking"] = opts.Thinking;
            if (opts.Deliver.HasValue)
                p["deliver"] = opts.Deliver.Value;
        }

        return p;
    }

    /// <summary>Wraps a subscription to the gateway's event stream.</summary>
    private sealed class EventSubscription : IAsyncDisposable, IAsyncEnumerable<StreamEvent>
    {
        private readonly CancellationTokenSource _cts;
        private readonly IAsyncEnumerable<StreamEvent> _inner;

        public EventSubscription(GatewayBase gateway, CancellationToken outerCt)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
            _inner = gateway.SubscribeAsync(ct: _cts.Token);
        }

        public IAsyncEnumerator<StreamEvent> GetAsyncEnumerator(CancellationToken ct = default)
            => _inner.GetAsyncEnumerator(ct);

        public ValueTask DisposeAsync() { _cts.Cancel(); _cts.Dispose(); return ValueTask.CompletedTask; }
    }
}
