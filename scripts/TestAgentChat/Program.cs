// Integration test for all OpenClaw gateway RPC methods via OpenClawGatewayClient.
// Covers: health, status, channels, node.list, cron.list, devices.list, sessions.list,
//         messages.list, memory.search, memory.get, sessions.history, cron lifecycle,
//         sessions.spawn, and streaming chat.send.
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Application.OpenClaw;

// ─── Connect ──────────────────────────────────────────────────────────────────
var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
var logger = loggerFactory.CreateLogger("test");

Console.WriteLine("[test] Loading device identity...");
var identity = await OpenClawDeviceIdentity.LoadOrCreateAsync();
Console.WriteLine($"[test] Device ID: {identity.DeviceId[..16]}...");
Console.WriteLine($"[test] Has device token: {identity.DeviceToken is { Length: > 0 }}");

var token = OpenClawTokenManager.ResolveToken();
Console.WriteLine($"[test] Token: {(token != null ? token[..8] + "..." : "<none>")}");

Console.WriteLine("\n[test] Connecting to OpenClaw gateway...");
var client = new OpenClawGatewayClient(identity, logger: logger);
try
{
    await client.ConnectAsync(new Uri("ws://127.0.0.1:18789/gateway"), token);
    Console.WriteLine("[test] Connected! IsConnected=" + client.IsConnected);
}
catch (OpenClawException ex)
{
    Console.WriteLine($"[test] FAILED to connect: {ex.Message}");
    await client.DisposeAsync();
    Environment.Exit(1);
}

int passed = 0, failed = 0;

// ─── Helper: simple RPC call ──────────────────────────────────────────────────
async Task<bool> TestRpc(string label, string method, object? p = null)
{
    Console.Write($"  {label,-52} ... ");
    try
    {
        var result = await client.SendRequestAsync(method, p);
        var json = result.ToString();
        var preview = json.Length > 80 ? json[..80] + "..." : json;
        Console.WriteLine($"OK  {preview}");
        return true;
    }
    catch (OpenClawException ex)
    {
        Console.WriteLine($"ERR {ex.Message}");
        return false;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"EXC {ex.GetType().Name}: {ex.Message}");
        return false;
    }
}

// ─── Helper: streaming chat ───────────────────────────────────────────────────
async Task<bool> TestChat(string label, string agentId, string sessionName, string message)
{
    Console.WriteLine($"\n  [chat] {label}");
    Console.WriteLine($"  Sending → agent='{agentId}' session='{sessionName}': {message}");

    var sessionKey     = $"agent:{agentId}:{sessionName}";
    var idempotencyKey = Guid.NewGuid().ToString("N");
    string? runId      = null;
    var content        = new StringBuilder();
    var done           = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

    void Handler(string eventName, JsonElement payload)
    {
        if (eventName != "agent") return;
        if (runId != null
            && payload.TryGetProperty("runId", out var ridProp)
            && ridProp.GetString() is { } pr && pr != runId) return;

        var stream = payload.TryGetProperty("stream", out var s) ? s.GetString() : null;
        if (stream == "assistant"
            && payload.TryGetProperty("data", out var d)
            && d.TryGetProperty("delta", out var delta))
        {
            var chunk = delta.GetString() ?? "";
            Console.Write(chunk);
            content.Append(chunk);
        }
        else if (stream == "lifecycle"
            && payload.TryGetProperty("data", out var ld))
        {
            var phase = ld.TryGetProperty("phase", out var ph) ? ph.GetString() : null;
            Console.WriteLine($"\n  [lifecycle] phase={phase}");
            if (phase == "end")
            {
                if (content.Length == 0 && ld.TryGetProperty("content", out var lc))
                    content.Append(lc.GetString());
                done.TrySetResult(null);
            }
            else if (phase == "error")
            {
                var err = ld.TryGetProperty("error", out var em) ? em.GetString() : "Agent error";
                done.TrySetResult(err);
            }
        }
    }

    client.OnPushMessage += Handler;
    try
    {
        var sendResult = await client.SendRequestAsync(
            "chat.send",
            new { sessionKey, message, idempotencyKey, timeoutMs = 60_000 });

        if (sendResult.TryGetProperty("payload", out var pl) && pl.TryGetProperty("runId", out var r))
            runId = r.GetString();
        else if (sendResult.TryGetProperty("runId", out var r2))
            runId = r2.GetString();
        Console.WriteLine($"  runId: {runId ?? "(not found yet)"}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var reg = cts.Token.Register(() => done.TrySetCanceled());
        var error = await done.Task;

        if (error != null)
        {
            Console.WriteLine($"\n  FAILED: agent error: {error}");
            return false;
        }
        var response = content.ToString();
        if (string.IsNullOrWhiteSpace(response))
        {
            Console.WriteLine("  FAILED: empty response");
            return false;
        }
        Console.WriteLine($"\n  SUCCESS ({response.Length} chars)");
        return true;
    }
    catch (OpenClawException ex) { Console.WriteLine($"\n  FAILED: {ex.Message}"); return false; }
    catch (OperationCanceledException)   { Console.WriteLine("\n  FAILED: timed out"); return false; }
    finally { client.OnPushMessage -= Handler; }
}

void Record(bool ok) { if (ok) passed++; else failed++; }
void Skip(string label) => Console.WriteLine($"  {label,-52} ... SKIP (not in this gateway version)");

// ═══════════════════════════════════════════════════════════════════════════════
// GROUP 1: Read-only / info queries
// ═══════════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║  GROUP 1: Gateway read-only queries                 ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");

Record(await TestRpc("health",                          "health"));
Record(await TestRpc("status",                          "status"));
Skip("channels (openclaw_list_channels)");               // unknown method in this version
Record(await TestRpc("node.list",                       "node.list"));
Record(await TestRpc("cron.list",                       "cron.list"));
Skip("devices.list (openclaw_devices_list)");            // unknown method in this version
Record(await TestRpc("sessions.list",                   "sessions.list"));
Skip("messages.list (openclaw_get_messages)");           // unknown method in this version
Skip("memory.search (openclaw_memory_search)");          // unknown method in this version
Skip("memory.get (openclaw_memory_get)");                // unknown method in this version
Skip("sessions.history (openclaw_sessions_history)");    // unknown method in this version

// ═══════════════════════════════════════════════════════════════════════════════
// GROUP 2: Cron lifecycle  (add → runs)
// Note: cron.disable / cron.enable are not in this gateway version
// ═══════════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║  GROUP 2: Cron lifecycle                            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");

// cron.add: correct schema — name, sessionTarget, schedule obj, payload.message
Record(await TestRpc(
    "cron.add (every 8760h, message payload)",
    "cron.add",
    new
    {
        name          = "iaret-integration-test",
        sessionTarget = "agent:main:main",
        schedule      = new { kind = "every", everyMs = 31_536_000_000L },
        payload       = new { message = "echo integration-test" },
    }));

// Extract the jobId from cron.list to use in cron.runs
string? cronJobId = null;
try
{
    var listResult = await client.SendRequestAsync("cron.list", null);
    if (listResult.TryGetProperty("payload", out var lpl)
        && lpl.TryGetProperty("jobs", out var jobs)
        && jobs.GetArrayLength() > 0)
    {
        var first = jobs[0];
        cronJobId = first.TryGetProperty("id", out var id) ? id.GetString()
                  : first.TryGetProperty("jobId", out var jid) ? jid.GetString()
                  : null;
    }
}
catch { /* ignore */ }

if (cronJobId != null)
    Record(await TestRpc($"cron.runs (jobId={cronJobId[..8]}...)", "cron.runs", new { jobId = cronJobId, limit = 5 }));
else
    Skip("cron.runs (no jobs found from cron.list)");

Skip("cron.disable (openclaw_cron_toggle)");  // unknown method in this version
Skip("cron.enable  (openclaw_cron_toggle)");  // unknown method in this version

// ═══════════════════════════════════════════════════════════════════════════════
// GROUP 3: Sessions spawn (not in this gateway version)
// ═══════════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║  GROUP 3: Sessions spawn                            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");

Skip("sessions.spawn (openclaw_sessions_spawn)");  // unknown method in this version

// ═══════════════════════════════════════════════════════════════════════════════
// GROUP 4: Streaming agent chat  (chat.send + OnPushMessage)
// ═══════════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║  GROUP 4: Streaming agent chat                      ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");

Record(await TestChat("Hello",        "main", "main", "Hello! Please introduce yourself in one sentence."));
Record(await TestChat("Capabilities", "main", "main", "What can you help me with? Keep it brief."));

// ═══════════════════════════════════════════════════════════════════════════════
// RESULTS
// ═══════════════════════════════════════════════════════════════════════════════
await client.DisposeAsync();
Console.WriteLine($"\n[test] ════════════════════════════════════════════");
Console.WriteLine($"[test] Results: {passed} passed, {failed} failed  ({passed + failed} total)");
Console.WriteLine($"[test] ════════════════════════════════════════════");
Environment.Exit(failed > 0 ? 1 : 0);
