// <copyright file="ProtocolGateway.cs" company="OpenClaw.Sdk">
// .NET port of the OpenClaw Python SDK (github.com/masteryodaa/openclaw-sdk)
// </copyright>

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace OpenClaw.Sdk.Gateway;

/// <summary>
/// WebSocket RPC gateway for the OpenClaw protocol.
///
/// Wire format:
///   Request:  {"type":"req","id":"req_1","method":"chat.send","params":{…}}
///   Response: {"id":"req_1","result":{…}}  or  {"id":"req_1","ok":false,"error":{…}}
///   Push:     {"type":"event","event":"chat","payload":{…}}
///
/// Auth flow (same as Ouroboros's OpenClawGatewayClient):
///   1. Gateway pushes connect.challenge with nonce
///   2. Client responds with connect RPC including bearer token
///   3. Gateway replies with ok:true (hello)
/// </summary>
public sealed class ProtocolGateway : GatewayBase
{
    private readonly string _wsUrl;
    private string? _token;
    private readonly float _connectTimeout;
    private readonly float _defaultTimeout;

    private ClientWebSocket _ws = new();
    private Task? _readerTask;
    private CancellationTokenSource _readerCts = new();

    private int _reqCounter;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly List<(IReadOnlyList<string>? filter, System.Threading.Channels.Channel<StreamEvent> ch)> _subscribers = [];
    private readonly SemaphoreSlim _subscriberLock = new(1, 1);
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private bool _connected;
    private readonly TaskCompletionSource _handshakeDone = new();

    public ProtocolGateway(
        string wsUrl = "ws://127.0.0.1:18789/gateway",
        string? token = null,
        float connectTimeout = 10f,
        float defaultTimeout = 30f)
    {
        _wsUrl = wsUrl;
        _token = token;
        _connectTimeout = connectTimeout;
        _defaultTimeout = defaultTimeout;
    }

    // ── Connection lifecycle ──────────────────────────────────────────────

    public override async Task ConnectAsync(CancellationToken ct = default)
    {
        _token ??= LoadToken();

        _ws = new ClientWebSocket();
        var uri = new Uri(_wsUrl);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_connectTimeout));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        await _ws.ConnectAsync(uri, linked.Token);

        _readerCts = new CancellationTokenSource();
        _readerTask = Task.Run(() => ReaderLoopAsync(_readerCts.Token), _readerCts.Token);

        // Wait for connect.challenge → connect RPC → hello-ok
        await _handshakeDone.Task.WaitAsync(TimeSpan.FromSeconds(_connectTimeout), ct);
        _connected = true;
    }

    public override async Task CloseAsync(CancellationToken ct = default)
    {
        _readerCts.Cancel();
        if (_ws.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", ct);
        _ws.Dispose();
        _connected = false;
    }

    public override Task<HealthStatus> HealthAsync(CancellationToken ct = default)
        => Task.FromResult(new HealthStatus(_connected, null, null));

    // ── Protocol primitives ───────────────────────────────────────────────

    public override async Task<JsonElement> CallAsync(
        string method,
        Dictionary<string, object?>? parameters = null,
        float? timeout = null,
        CancellationToken ct = default)
    {
        var id = $"req_{Interlocked.Increment(ref _reqCounter)}";
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var envelope = new Dictionary<string, object?>
        {
            ["type"] = "req",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters ?? [],
        };

        var json = JsonSerializer.Serialize(envelope);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(ct);
        try
        {
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }

        var deadline = TimeSpan.FromSeconds(timeout ?? _defaultTimeout);
        try
        {
            return await tcs.Task.WaitAsync(deadline, ct);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    public override async IAsyncEnumerable<StreamEvent> SubscribeAsync(
        IReadOnlyList<string>? eventTypes = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var ch = System.Threading.Channels.Channel.CreateUnbounded<StreamEvent>();

        await _subscriberLock.WaitAsync(ct);
        try { _subscribers.Add((eventTypes, ch)); }
        finally { _subscriberLock.Release(); }

        try
        {
            await foreach (var ev in ch.Reader.ReadAllAsync(ct))
                yield return ev;
        }
        finally
        {
            await _subscriberLock.WaitAsync(CancellationToken.None);
            try { _subscribers.RemoveAll(s => s.ch == ch); }
            finally { _subscriberLock.Release(); }
        }
    }

    // ── Reader loop ───────────────────────────────────────────────────────

    private async Task ReaderLoopAsync(CancellationToken ct)
    {
        var buf = new byte[65536];
        using var ms = new MemoryStream();

        while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try { result = await _ws.ReceiveAsync(buf, ct); }
            catch (OperationCanceledException) { break; }
            catch (WebSocketException) { break; }

            if (result.MessageType == WebSocketMessageType.Close) break;

            ms.Write(buf, 0, result.Count);
            if (!result.EndOfMessage) continue;

            var json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            ms.SetLength(0);

            try { RouteMessage(json); }
            catch { /* swallow per-message parse errors */ }
        }
    }

    private void RouteMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();

        // Is it a push event?
        if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "event")
        {
            var eventName = root.TryGetProperty("event", out var ev) ? ev.GetString() ?? "" : "";

            // connect.challenge — initiate handshake
            if (eventName == "connect.challenge")
            {
                _ = Task.Run(() => SendConnectHandshakeAsync(root));
                return;
            }

            var eventType = MapEventType(eventName);
            var payload = root.TryGetProperty("payload", out var pl)
                ? pl.Deserialize<Dictionary<string, object?>>() ?? []
                : new Dictionary<string, object?>();
            var ev_ = new StreamEvent(eventType, payload);
            BroadcastEvent(ev_, eventName);
            return;
        }

        // Response to a pending request
        if (!root.TryGetProperty("id", out var idProp)) return;
        var id = idProp.GetString();
        if (id == null || !_pending.TryRemove(id, out var tcs)) return;

        if (root.TryGetProperty("ok", out var ok) && !ok.GetBoolean()
            || root.TryGetProperty("error", out _))
        {
            var msg = root.TryGetProperty("error", out var errEl)
                && errEl.TryGetProperty("message", out var msgEl)
                ? msgEl.GetString() ?? "Gateway error"
                : "Gateway error";
            tcs.TrySetException(new GatewayException(msg));
        }
        else
        {
            var result = root.TryGetProperty("result", out var r) ? r : root;
            tcs.TrySetResult(result);
        }
    }

    private async Task SendConnectHandshakeAsync(JsonElement challengeRoot)
    {
        var nonce = challengeRoot.TryGetProperty("payload", out var pl)
            && pl.TryGetProperty("nonce", out var n) ? n.GetString() : null;

        var connectParams = new Dictionary<string, object?>
        {
            ["minProtocol"] = 3,
            ["maxProtocol"] = 3,
            ["role"] = "operator",
            ["scopes"] = new[] { "operator.read", "operator.write", "operator.admin" },
            ["client"] = new Dictionary<string, object?>
            {
                ["id"] = "openclaw-sdk-dotnet",
                ["version"] = "1.0.0",
                ["platform"] = Environment.OSVersion.Platform.ToString().ToLowerInvariant(),
                ["mode"] = "backend",
            },
        };

        if (!string.IsNullOrEmpty(_token))
            connectParams["auth"] = new Dictionary<string, object?> { ["token"] = _token };

        if (nonce != null)
            connectParams["nonce"] = nonce;

        var id = $"req_{Interlocked.Increment(ref _reqCounter)}";
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var envelope = new Dictionary<string, object?>
        {
            ["type"] = "req",
            ["id"] = id,
            ["method"] = "connect",
            ["params"] = connectParams,
        };

        var json = JsonSerializer.Serialize(envelope);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _sendLock.WaitAsync();
        try { await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
        finally { _sendLock.Release(); }

        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(_connectTimeout));
            _handshakeDone.TrySetResult();
        }
        catch (Exception ex)
        {
            _handshakeDone.TrySetException(new GatewayException($"Handshake failed: {ex.Message}"));
        }
    }

    private void BroadcastEvent(StreamEvent ev, string rawName)
    {
        _subscriberLock.Wait();
        try
        {
            foreach (var (filter, ch) in _subscribers)
            {
                if (filter == null || filter.Contains(rawName))
                    ch.Writer.TryWrite(ev);
            }
        }
        finally { _subscriberLock.Release(); }
    }

    private static EventType MapEventType(string name) => name switch
    {
        "chat"      => EventType.Chat,
        "agent"     => EventType.Agent,
        "presence"  => EventType.Presence,
        "health"    => EventType.Health,
        "tick"      => EventType.Tick,
        "heartbeat" => EventType.Heartbeat,
        "cron"      => EventType.Cron,
        "shutdown"  => EventType.Shutdown,
        "done"      => EventType.Done,
        "error"     => EventType.Error,
        "content"   => EventType.Content,
        _           => EventType.Agent,
    };

    // ── Token loading ─────────────────────────────────────────────────────

    internal static string? LoadToken()
    {
        var env = Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY_TOKEN");
        if (!string.IsNullOrEmpty(env)) return env;

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".openclaw", "openclaw.json");

        if (!File.Exists(path)) return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement
                .TryGetProperty("gateway", out var gw)
                && gw.TryGetProperty("auth", out var auth)
                && auth.TryGetProperty("token", out var tok)
                ? tok.GetString()
                : null;
        }
        catch { return null; }
    }
}
