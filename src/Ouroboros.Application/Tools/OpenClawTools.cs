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
    /// <summary>Shared gateway client, set during ToolSubsystem init.</summary>
    public static OpenClawGatewayClient? SharedClient { get; set; }

    /// <summary>Shared security policy for message/node validation.</summary>
    public static OpenClawSecurityPolicy? SharedPolicy { get; set; }

    /// <summary>Gets all OpenClaw tools.</summary>
    public static IEnumerable<ITool> GetAllTools()
    {
        yield return new OpenClawStatusTool();
        yield return new OpenClawListChannelsTool();
        yield return new OpenClawNodeListTool();
        yield return new OpenClawSendMessageTool();
        yield return new OpenClawNodeInvokeTool();
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

            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;

                var channel = root.GetProperty("channel").GetString() ?? "";
                var to = root.GetProperty("to").GetString() ?? "";
                var message = root.GetProperty("message").GetString() ?? "";

                // Security policy check
                if (SharedPolicy != null)
                {
                    var verdict = SharedPolicy.ValidateSendMessage(channel, to, message);
                    if (!verdict.IsAllowed)
                        return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");
                }

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

            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;

                var node = root.GetProperty("node").GetString() ?? "";
                var command = root.GetProperty("command").GetString() ?? "";
                var paramsJson = root.TryGetProperty("params", out var p) ? p.ToString() : null;

                // Security policy check
                if (SharedPolicy != null)
                {
                    var verdict = SharedPolicy.ValidateNodeInvoke(node, command, paramsJson);
                    if (!verdict.IsAllowed)
                        return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");
                }

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
}
