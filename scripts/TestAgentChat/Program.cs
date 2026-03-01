// Direct integration test for OpenClawGatewayClient: connect, chat.send, stream events
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Application.OpenClaw;

var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = loggerFactory.CreateLogger("test");

Console.WriteLine("[test] Loading device identity...");
var identity = await OpenClawDeviceIdentity.LoadOrCreateAsync();
Console.WriteLine($"[test] Device ID: {identity.DeviceId[..16]}...");
Console.WriteLine($"[test] Has device token: {identity.DeviceToken is { Length: > 0 }}");

var token = OpenClawTokenManager.ResolveToken();
Console.WriteLine($"[test] Token resolved: {(token != null ? token[..8] + "..." : "<none>")}");

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

// Test: send chat.send and collect streaming response
int passed = 0, failed = 0;

async Task<bool> TestChat(string label, string agentId, string sessionName, string message)
{
    Console.WriteLine($"\n[test] === {label} ===");
    Console.WriteLine($"[test] Sending to agent '{agentId}' session '{sessionName}': {message}");

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
            && ridProp.GetString() is { } pr && pr != runId)
            return;

        var stream = payload.TryGetProperty("stream", out var s) ? s.GetString() : null;

        if (stream == "assistant"
            && payload.TryGetProperty("data", out var d)
            && d.TryGetProperty("delta", out var delta))
        {
            var chunk = delta.GetString() ?? "";
            Console.Write(chunk);  // live streaming
            content.Append(chunk);
        }
        else if (stream == "lifecycle"
            && payload.TryGetProperty("data", out var ld))
        {
            var phase = ld.TryGetProperty("phase", out var p) ? p.GetString() : null;
            Console.WriteLine($"\n[lifecycle] phase={phase}");
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
            new
            {
                sessionKey,
                message,
                idempotencyKey,
                timeoutMs = 60_000,
            });

        Console.WriteLine($"[test] chat.send returned: {sendResult}");

        if (sendResult.TryGetProperty("payload", out var pl) && pl.TryGetProperty("runId", out var r))
            runId = r.GetString();
        else if (sendResult.TryGetProperty("runId", out var r2))
            runId = r2.GetString();
        Console.WriteLine($"[test] runId: {runId ?? "(not found yet)"}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var reg = cts.Token.Register(() => done.TrySetCanceled());

        var error = await done.Task;
        if (error != null)
        {
            Console.WriteLine($"\n[test] FAILED: agent error: {error}");
            return false;
        }

        var response = content.ToString();
        if (string.IsNullOrWhiteSpace(response))
        {
            Console.WriteLine("[test] FAILED: empty response");
            return false;
        }

        Console.WriteLine($"\n[test] SUCCESS ({response.Length} chars)");
        return true;
    }
    catch (OpenClawException ex)
    {
        Console.WriteLine($"\n[test] FAILED: gateway exception: {ex.Message}");
        return false;
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("\n[test] FAILED: timed out waiting for response");
        return false;
    }
    finally
    {
        client.OnPushMessage -= Handler;
    }
}

if (await TestChat("Test 1: Hello", "main", "main", "Hello! Please introduce yourself briefly."))
    passed++;
else
    failed++;

if (await TestChat("Test 2: Capabilities", "main", "main", "What can you help me with?"))
    passed++;
else
    failed++;

await client.DisposeAsync();

Console.WriteLine($"\n[test] Results: {passed} passed, {failed} failed");
Environment.Exit(failed > 0 ? 1 : 0);
