// <copyright file="OpenClawTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// OpenClaw Gateway tools — gives the agent a communication nervous system
/// through WhatsApp, Telegram, Slack, Discord, Signal, iMessage, and device nodes.
/// </summary>
public static class OpenClawTools
{
    private const string DefaultGateway = Configuration.DefaultEndpoints.OpenClawGateway;

    /// <summary>Shared gateway client, set during init.</summary>
    public static OpenClawGatewayClient? SharedClient { get; set; }

    /// <summary>Shared security policy for message/node validation.</summary>
    public static OpenClawSecurityPolicy? SharedPolicy { get; set; }

    /// <summary>
    /// Connects to the OpenClaw Gateway, sets up security, and populates
    /// <see cref="SharedClient"/> / <see cref="SharedPolicy"/>.
    /// CLI option values take precedence; env vars are used as fallback.
    /// </summary>
    /// <param name="gatewayUrl">Explicit gateway URL (from --openclaw-gateway), or null for env/default.</param>
    /// <param name="token">Explicit token (from --openclaw-token), or null for env.</param>
    /// <returns>The resolved gateway URL on success, or null if connection failed.</returns>
    public static async Task<string?> ConnectGatewayAsync(
        string? gatewayUrl = null,
        string? token = null)
    {
        string resolvedGateway = gatewayUrl
            ?? Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY")
            ?? DefaultGateway;
        string? resolvedToken = token
            ?? Environment.GetEnvironmentVariable("OPENCLAW_TOKEN");

        var deviceIdentity = await OpenClawDeviceIdentity.LoadOrCreateAsync();
        var client = new OpenClawGatewayClient(deviceIdentity);
        await client.ConnectAsync(
            new Uri(resolvedGateway),
            resolvedToken,
            CancellationToken.None);
        SharedClient = client;

        var auditLog = new OpenClawAuditLog();
        var securityConfig = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development"
            ? OpenClawSecurityConfig.CreateDevelopment()
            : OpenClawSecurityConfig.CreateDefault();
        SharedPolicy = new OpenClawSecurityPolicy(securityConfig, auditLog);

        return resolvedGateway;
    }

    /// <summary>Gets all OpenClaw tools.</summary>
    public static IEnumerable<ITool> GetAllTools()
    {
        // Core operator tools
        yield return new OpenClawStatusTool();
        yield return new OpenClawListChannelsTool();
        yield return new OpenClawNodeListTool();
        yield return new OpenClawSendMessageTool();
        yield return new OpenClawNodeInvokeTool();

        // Session management
        yield return new OpenClawSessionsListTool();
        yield return new OpenClawSessionsHistoryTool();
        yield return new OpenClawSessionsSendTool();
        yield return new OpenClawSessionsSpawnTool();

        // Memory
        yield return new OpenClawMemorySearchTool();
        yield return new OpenClawMemoryGetTool();

        // Hardware / node shortcuts
        yield return new OpenClawCameraSnapTool();
        yield return new OpenClawCameraClipTool();
        yield return new OpenClawLocationGetTool();
        yield return new OpenClawScreenRecordNodeTool();

        // Scheduling
        yield return new OpenClawCronListTool();
        yield return new OpenClawCronAddTool();
        yield return new OpenClawCronToggleTool();
        yield return new OpenClawCronRunsTool();

        // Gateway management
        yield return new OpenClawHealthTool();
        yield return new OpenClawDevicesListTool();
        yield return new OpenClawDevicesApproveTool();
        yield return new OpenClawDevicesRevokeTool();

        // Incoming messages
        yield return new OpenClawGetMessagesTool();
        yield return new OpenClawPollMessagesTool();
    }

    /// <summary>Adds OpenClaw tools to a registry.</summary>
    public static ToolRegistry WithOpenClawTools(this ToolRegistry registry)
    {
        foreach (var tool in GetAllTools())
            registry = registry.WithTool(tool);
        return registry;
    }

    private static Result<string, string> NotConnected() =>
        Result<string, string>.Failure("OpenClaw Gateway is not connected. Ensure the gateway is running and --enable-openclaw is set.");

    private static Result<string, string> PolicyNotInitialized() =>
        Result<string, string>.Failure("OpenClaw security policy not initialized. Write operations are blocked until the policy is configured.");

    // ═════════════════════════════════════════════════════════════════════════════
    // openclaw_status
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawStatusTool : ITool
    {
        public string Name => "openclaw_status";
        public string Description => "Get OpenClaw Gateway health and connection info including circuit breaker states.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();

            try
            {
                var result = await SharedClient.SendRequestAsync("status", null, ct);
                var status = result.ToString();
                var resilience = SharedClient.Resilience.GetStatusSummary();
                return Result<string, string>.Success($"Gateway status: {status}\nResilience: {resilience}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get gateway status: {ex.Message}");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // openclaw_list_channels
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawListChannelsTool : ITool
    {
        public string Name => "openclaw_list_channels";
        public string Description => "List active messaging channels and their status (WhatsApp, Telegram, Slack, Discord, etc.).";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();

            try
            {
                var result = await SharedClient.SendRequestAsync("channels", null, ct);
                return Result<string, string>.Success(result.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to list channels: {ex.Message}");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // openclaw_node_list
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawNodeListTool : ITool
    {
        public string Name => "openclaw_node_list";
        public string Description => "List connected device nodes with their capabilities (camera, SMS, location, etc.).";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();

            try
            {
                var result = await SharedClient.SendRequestAsync("node.list", null, ct);
                return Result<string, string>.Success(result.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to list nodes: {ex.Message}");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // openclaw_send_message
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawSendMessageTool : ITool
    {
        public string Name => "openclaw_send_message";
        public string Description => "Send a message through any OpenClaw channel (WhatsApp, Telegram, Slack, Discord, Signal, iMessage, etc.).";
        public string? JsonSchema => """{"type":"object","properties":{"channel":{"type":"string","description":"Channel name (whatsapp, telegram, slack, discord, signal, imessage, etc.)"},"to":{"type":"string","description":"Recipient identifier (phone number, username, channel ID)"},"message":{"type":"string","description":"Message text to send"}},"required":["channel","to","message"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            if (SharedPolicy == null)
                return PolicyNotInitialized();

            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;

                var channel = root.GetProperty("channel").GetString() ?? "";
                var to = root.GetProperty("to").GetString() ?? "";
                var message = root.GetProperty("message").GetString() ?? "";

                // Security policy check (mandatory)
                var verdict = SharedPolicy.ValidateSendMessage(channel, to, message);
                if (!verdict.IsAllowed)
                    return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

                var result = await SharedClient.SendRequestAsync("chat.send", new
                {
                    channel,
                    to,
                    message,
                }, ct);

                return Result<string, string>.Success($"Message sent via {channel} to {to}. Response: {result}");
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON input. Expected: {\"channel\":\"...\",\"to\":\"...\",\"message\":\"...\"}");
            }
            catch (OpenClawException ex)
            {
                return Result<string, string>.Failure($"Gateway error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to send message: {ex.Message}");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // openclaw_node_invoke
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawNodeInvokeTool : ITool
    {
        public string Name => "openclaw_node_invoke";
        public string Description => "Execute an action on a connected device node (camera.snap, sms.send, location.get, screen.record, etc.).";
        public string? JsonSchema => """{"type":"object","properties":{"node":{"type":"string","description":"Node identifier (e.g. 'phone', 'macbook')"},"command":{"type":"string","description":"Command to execute (camera.snap, sms.send, location.get, etc.)"},"params":{"type":"object","description":"Optional command parameters"}},"required":["node","command"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            if (SharedPolicy == null)
                return PolicyNotInitialized();

            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;

                var node = root.GetProperty("node").GetString() ?? "";
                var command = root.GetProperty("command").GetString() ?? "";
                var paramsJson = root.TryGetProperty("params", out var p) ? p.ToString() : null;

                // Security policy check (mandatory)
                var verdict = SharedPolicy.ValidateNodeInvoke(node, command, paramsJson);
                if (!verdict.IsAllowed)
                    return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

                var result = await SharedClient.SendRequestAsync("node.invoke", new
                {
                    node,
                    command,
                    @params = paramsJson != null ? JsonSerializer.Deserialize<JsonElement>(paramsJson) : (object?)null,
                }, ct);

                return Result<string, string>.Success($"Node invoke result ({node}/{command}): {result}");
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON input. Expected: {\"node\":\"...\",\"command\":\"...\",\"params\":{}}");
            }
            catch (OpenClawException ex)
            {
                return Result<string, string>.Failure($"Gateway error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to invoke node command: {ex.Message}");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // SESSION MANAGEMENT
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawSessionsListTool : ITool
    {
        public string Name => "openclaw_sessions_list";
        public string Description => "List active OpenClaw sessions with agents, channels, and timestamps.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            try
            {
                var result = await SharedClient.SendRequestAsync("sessions.list", null, ct);
                return Result<string, string>.Success(result.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to list sessions: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawSessionsHistoryTool : ITool
    {
        public string Name => "openclaw_sessions_history";
        public string Description => "Get message history for an OpenClaw session.";
        public string? JsonSchema => """{"type":"object","properties":{"sessionId":{"type":"string","description":"Session ID to retrieve history for"},"limit":{"type":"integer","description":"Maximum messages to return (default: 50)"}},"required":["sessionId"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                var sessionId = root.GetProperty("sessionId").GetString() ?? "";
                var limit = root.TryGetProperty("limit", out var l) ? l.GetInt32() : 50;

                var result = await SharedClient.SendRequestAsync("sessions.history", new { sessionId, limit }, ct);
                return Result<string, string>.Success(result.ToString());
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"sessionId\":\"...\", \"limit\":50}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get session history: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawSessionsSendTool : ITool
    {
        public string Name => "openclaw_sessions_send";
        public string Description => "Inject a message into an existing OpenClaw session.";
        public string? JsonSchema => """{"type":"object","properties":{"sessionId":{"type":"string","description":"Target session ID"},"message":{"type":"string","description":"Message to inject"}},"required":["sessionId","message"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            if (SharedPolicy == null)
                return PolicyNotInitialized();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                var sessionId = root.GetProperty("sessionId").GetString() ?? "";
                var message = root.GetProperty("message").GetString() ?? "";

                // Scan session messages for sensitive data (same as send_message)
                var verdict = SharedPolicy.ValidateSendMessage("session", sessionId, message);
                if (!verdict.IsAllowed)
                    return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

                var result = await SharedClient.SendRequestAsync("sessions.send", new { sessionId, message }, ct);
                return Result<string, string>.Success($"Message injected into session {sessionId}. Response: {result}");
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"sessionId\":\"...\", \"message\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to send to session: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawSessionsSpawnTool : ITool
    {
        public string Name => "openclaw_sessions_spawn";
        public string Description => "Spawn a new OpenClaw agent session with a prompt.";
        public string? JsonSchema => """{"type":"object","properties":{"prompt":{"type":"string","description":"Initial prompt for the new session"},"channel":{"type":"string","description":"Optional channel to bind the session to"}},"required":["prompt"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            if (SharedPolicy == null)
                return PolicyNotInitialized();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                var prompt = root.GetProperty("prompt").GetString() ?? "";
                var channel = root.TryGetProperty("channel", out var ch) ? ch.GetString() : null;

                var p = channel != null
                    ? (object)new { prompt, channel }
                    : new { prompt };

                var result = await SharedClient.SendRequestAsync("sessions.spawn", p, ct);
                return Result<string, string>.Success($"Session spawned. Response: {result}");
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"prompt\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to spawn session: {ex.Message}");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // MEMORY
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawMemorySearchTool : ITool
    {
        public string Name => "openclaw_memory_search";
        public string Description => "Semantic search across OpenClaw's persistent knowledge files (MEMORY.md, memory/*.md).";
        public string? JsonSchema => """{"type":"object","properties":{"query":{"type":"string","description":"Search query"},"limit":{"type":"integer","description":"Maximum results (default: 10)"}},"required":["query"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                var query = root.GetProperty("query").GetString() ?? "";
                var limit = root.TryGetProperty("limit", out var l) ? l.GetInt32() : 10;

                var result = await SharedClient.SendRequestAsync("memory.search", new { query, limit }, ct);
                return Result<string, string>.Success(result.ToString());
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"query\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to search memory: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawMemoryGetTool : ITool
    {
        public string Name => "openclaw_memory_get";
        public string Description => "Retrieve a specific memory entry by key from OpenClaw's knowledge store.";
        public string? JsonSchema => """{"type":"object","properties":{"key":{"type":"string","description":"Memory entry key"}},"required":["key"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var key = doc.RootElement.GetProperty("key").GetString() ?? "";

                var result = await SharedClient.SendRequestAsync("memory.get", new { key }, ct);
                return Result<string, string>.Success(result.ToString());
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"key\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get memory entry: {ex.Message}");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // HARDWARE / NODE SHORTCUTS
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawCameraSnapTool : ITool
    {
        public string Name => "openclaw_camera_snap";
        public string Description => "Take a photo on a paired phone/camera node. Returns the image data.";
        public string? JsonSchema => """{"type":"object","properties":{"node":{"type":"string","description":"Node identifier (e.g. 'phone')"},"camera":{"type":"string","description":"Camera to use: 'front' or 'back' (default: 'back')"}},"required":["node"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            if (SharedPolicy == null)
                return PolicyNotInitialized();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                var node = root.GetProperty("node").GetString() ?? "";
                var camera = root.TryGetProperty("camera", out var c) ? c.GetString() ?? "back" : "back";

                var verdict = SharedPolicy.ValidateNodeInvoke(node, "camera.snap", null);
                if (!verdict.IsAllowed)
                    return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

                var result = await SharedClient.SendRequestAsync("node.invoke", new
                {
                    node,
                    command = "camera.snap",
                    @params = new { camera },
                }, ct);
                return Result<string, string>.Success($"Camera snap result: {result}");
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"node\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to snap camera: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawCameraClipTool : ITool
    {
        public string Name => "openclaw_camera_clip";
        public string Description => "Record a short video clip on a paired phone/camera node.";
        public string? JsonSchema => """{"type":"object","properties":{"node":{"type":"string","description":"Node identifier"},"duration":{"type":"integer","description":"Duration in seconds (default: 5, max: 30)"},"camera":{"type":"string","description":"Camera: 'front' or 'back' (default: 'back')"}},"required":["node"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            if (SharedPolicy == null)
                return PolicyNotInitialized();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                var node = root.GetProperty("node").GetString() ?? "";
                var duration = root.TryGetProperty("duration", out var d) ? d.GetInt32() : 5;
                var camera = root.TryGetProperty("camera", out var c) ? c.GetString() ?? "back" : "back";

                var verdict = SharedPolicy.ValidateNodeInvoke(node, "camera.clip", null);
                if (!verdict.IsAllowed)
                    return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

                var result = await SharedClient.SendRequestAsync("node.invoke", new
                {
                    node,
                    command = "camera.clip",
                    @params = new { duration, camera },
                }, ct);
                return Result<string, string>.Success($"Camera clip result: {result}");
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"node\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to record camera clip: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawLocationGetTool : ITool
    {
        public string Name => "openclaw_location_get";
        public string Description => "Get GPS location from a paired phone node.";
        public string? JsonSchema => """{"type":"object","properties":{"node":{"type":"string","description":"Node identifier (e.g. 'phone')"}},"required":["node"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            if (SharedPolicy == null)
                return PolicyNotInitialized();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var node = doc.RootElement.GetProperty("node").GetString() ?? "";

                var verdict = SharedPolicy.ValidateNodeInvoke(node, "location.get", null);
                if (!verdict.IsAllowed)
                    return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

                var result = await SharedClient.SendRequestAsync("node.invoke", new
                {
                    node,
                    command = "location.get",
                }, ct);
                return Result<string, string>.Success($"Location: {result}");
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"node\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get location: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawScreenRecordNodeTool : ITool
    {
        public string Name => "openclaw_screen_record_node";
        public string Description => "Record screen on a remote device node.";
        public string? JsonSchema => """{"type":"object","properties":{"node":{"type":"string","description":"Node identifier"},"duration":{"type":"integer","description":"Duration in seconds (default: 10, max: 60)"}},"required":["node"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            if (SharedPolicy == null)
                return PolicyNotInitialized();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                var node = root.GetProperty("node").GetString() ?? "";
                var duration = root.TryGetProperty("duration", out var d) ? d.GetInt32() : 10;

                var verdict = SharedPolicy.ValidateNodeInvoke(node, "screen.record", null);
                if (!verdict.IsAllowed)
                    return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

                var result = await SharedClient.SendRequestAsync("node.invoke", new
                {
                    node,
                    command = "screen.record",
                    @params = new { duration },
                }, ct);
                return Result<string, string>.Success($"Screen recording result: {result}");
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"node\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to record screen: {ex.Message}");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // SCHEDULING (CRON)
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawCronListTool : ITool
    {
        public string Name => "openclaw_cron_list";
        public string Description => "List all scheduled jobs in OpenClaw.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            try
            {
                var result = await SharedClient.SendRequestAsync("cron.list", null, ct);
                return Result<string, string>.Success(result.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to list cron jobs: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawCronAddTool : ITool
    {
        public string Name => "openclaw_cron_add";
        public string Description => "Create a scheduled job in OpenClaw. Supports at/every/cron syntax.";
        public string? JsonSchema => """{"type":"object","properties":{"name":{"type":"string","description":"Job name"},"schedule":{"type":"string","description":"Schedule expression (e.g. 'every 1h', 'at 09:00', '0 */2 * * *')"},"action":{"type":"string","description":"Action to execute (prompt or command)"},"channel":{"type":"string","description":"Optional channel to run in"}},"required":["name","schedule","action"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            if (SharedPolicy == null)
                return PolicyNotInitialized();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                var name = root.GetProperty("name").GetString() ?? "";
                var schedule = root.GetProperty("schedule").GetString() ?? "";
                var action = root.GetProperty("action").GetString() ?? "";
                var channel = root.TryGetProperty("channel", out var ch) ? ch.GetString() : null;

                var p = channel != null
                    ? (object)new { name, schedule, action, channel }
                    : new { name, schedule, action };

                var result = await SharedClient.SendRequestAsync("cron.add", p, ct);
                return Result<string, string>.Success($"Job '{name}' created. Response: {result}");
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"name\":\"...\", \"schedule\":\"...\", \"action\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to add cron job: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawCronToggleTool : ITool
    {
        public string Name => "openclaw_cron_toggle";
        public string Description => "Enable or disable a scheduled job.";
        public string? JsonSchema => """{"type":"object","properties":{"name":{"type":"string","description":"Job name"},"enabled":{"type":"boolean","description":"true to enable, false to disable"}},"required":["name","enabled"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            if (SharedPolicy == null)
                return PolicyNotInitialized();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                var name = root.GetProperty("name").GetString() ?? "";
                var enabled = root.GetProperty("enabled").GetBoolean();

                var method = enabled ? "cron.enable" : "cron.disable";
                var result = await SharedClient.SendRequestAsync(method, new { name }, ct);
                return Result<string, string>.Success($"Job '{name}' {(enabled ? "enabled" : "disabled")}. Response: {result}");
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"name\":\"...\", \"enabled\":true}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to toggle cron job: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawCronRunsTool : ITool
    {
        public string Name => "openclaw_cron_runs";
        public string Description => "View execution history for a scheduled job.";
        public string? JsonSchema => """{"type":"object","properties":{"name":{"type":"string","description":"Job name"},"limit":{"type":"integer","description":"Maximum runs to return (default: 10)"}},"required":["name"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                var name = root.GetProperty("name").GetString() ?? "";
                var limit = root.TryGetProperty("limit", out var l) ? l.GetInt32() : 10;

                var result = await SharedClient.SendRequestAsync("cron.runs", new { name, limit }, ct);
                return Result<string, string>.Success(result.ToString());
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"name\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get cron runs: {ex.Message}");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // GATEWAY MANAGEMENT
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawHealthTool : ITool
    {
        public string Name => "openclaw_health";
        public string Description => "Get detailed OpenClaw gateway health including provider status, uptime, and connected nodes.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            try
            {
                var result = await SharedClient.SendRequestAsync("health", null, ct);
                return Result<string, string>.Success(result.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get gateway health: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawDevicesListTool : ITool
    {
        public string Name => "openclaw_devices_list";
        public string Description => "List all paired devices with their roles (operator, node) and connection status.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            try
            {
                var result = await SharedClient.SendRequestAsync("devices.list", null, ct);
                return Result<string, string>.Success(result.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to list devices: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawDevicesApproveTool : ITool
    {
        public string Name => "openclaw_devices_approve";
        public string Description => "Approve a pending device pairing request.";
        public string? JsonSchema => """{"type":"object","properties":{"deviceId":{"type":"string","description":"Device ID to approve"}},"required":["deviceId"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            if (SharedPolicy == null)
                return PolicyNotInitialized();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var deviceId = doc.RootElement.GetProperty("deviceId").GetString() ?? "";

                var result = await SharedClient.SendRequestAsync("devices.approve", new { deviceId }, ct);
                return Result<string, string>.Success($"Device '{deviceId}' approved. Response: {result}");
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"deviceId\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to approve device: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawDevicesRevokeTool : ITool
    {
        public string Name => "openclaw_devices_revoke";
        public string Description => "Revoke a paired device's access to the gateway.";
        public string? JsonSchema => """{"type":"object","properties":{"deviceId":{"type":"string","description":"Device ID to revoke"}},"required":["deviceId"]}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            if (SharedPolicy == null)
                return PolicyNotInitialized();
            try
            {
                using var doc = JsonDocument.Parse(input);
                var deviceId = doc.RootElement.GetProperty("deviceId").GetString() ?? "";

                var result = await SharedClient.SendRequestAsync("devices.revoke", new { deviceId }, ct);
                return Result<string, string>.Success($"Device '{deviceId}' revoked. Response: {result}");
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"deviceId\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to revoke device: {ex.Message}");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // INCOMING MESSAGES
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawGetMessagesTool : ITool
    {
        public string Name => "openclaw_get_messages";
        public string Description => "Get recent incoming messages from OpenClaw channels.";
        public string? JsonSchema => """{"type":"object","properties":{"channel":{"type":"string","description":"Optional channel filter (e.g. 'whatsapp', 'telegram')"},"limit":{"type":"integer","description":"Maximum messages (default: 20)"}}}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            try
            {
                object? p = null;
                if (!string.IsNullOrWhiteSpace(input))
                {
                    using var doc = JsonDocument.Parse(input);
                    var root = doc.RootElement;
                    var channel = root.TryGetProperty("channel", out var ch) ? ch.GetString() : null;
                    var limit = root.TryGetProperty("limit", out var l) ? l.GetInt32() : 20;
                    p = channel != null ? (object)new { channel, limit } : new { limit };
                }

                var result = await SharedClient.SendRequestAsync("messages.list", p, ct);
                return Result<string, string>.Success(result.ToString());
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"channel\":\"...\", \"limit\":20}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get messages: {ex.Message}");
            }
        }
    }

    public sealed class OpenClawPollMessagesTool : ITool
    {
        public string Name => "openclaw_poll_messages";
        public string Description => "Wait for new incoming messages with a timeout. Returns when messages arrive or timeout expires.";
        public string? JsonSchema => """{"type":"object","properties":{"channel":{"type":"string","description":"Optional channel filter"},"timeout":{"type":"integer","description":"Timeout in seconds (default: 30, max: 120)"}}}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedClient == null || !SharedClient.IsConnected)
                return NotConnected();
            try
            {
                var timeout = 30;
                string? channel = null;

                if (!string.IsNullOrWhiteSpace(input))
                {
                    using var doc = JsonDocument.Parse(input);
                    var root = doc.RootElement;
                    channel = root.TryGetProperty("channel", out var ch) ? ch.GetString() : null;
                    timeout = root.TryGetProperty("timeout", out var t) ? Math.Min(t.GetInt32(), 120) : 30;
                }

                var p = channel != null
                    ? (object)new { channel, timeout }
                    : new { timeout };

                var result = await SharedClient.SendRequestAsync("messages.poll", p, ct);
                return Result<string, string>.Success(result.ToString());
            }
            catch (JsonException)
            {
                return Result<string, string>.Failure("Invalid JSON. Expected: {\"channel\":\"...\", \"timeout\":30}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to poll messages: {ex.Message}");
            }
        }
    }
}
