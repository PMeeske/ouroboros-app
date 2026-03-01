// <copyright file="OpenClawAgentChatTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using OpenClaw.Sdk;
using OpenClaw.Sdk.Config;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Allows Iaret to send a query to a named OpenClaw gateway agent and receive its response.
/// Uses <c>OpenClaw.Sdk.OpenClawClient</c> for streaming execution.
///
/// Example input: {"agentId":"main","query":"What is the weather today?"}
/// </summary>
public sealed class OpenClawAgentChatTool : ITool
{
    public string Name => "openclaw_agent_chat";

    public string Description =>
        "Send a message to a named OpenClaw gateway agent and return its response. " +
        "agentId defaults to 'main'. Use this to communicate with agents registered in the local OpenClaw gateway.";

    public string? JsonSchema => """
        {
          "type": "object",
          "properties": {
            "agentId":   { "type": "string", "description": "Gateway agent ID (default: 'main')" },
            "query":     { "type": "string", "description": "Message to send to the agent" },
            "sessionName": { "type": "string", "description": "Session name (default: 'main')" },
            "timeoutSeconds": { "type": "integer", "description": "Response timeout in seconds (default: 120)" }
          },
          "required": ["query"]
        }
        """;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        string agentId, query, sessionName;
        int timeoutSeconds;

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;
            agentId     = root.TryGetProperty("agentId", out var aid)     ? aid.GetString() ?? "main"  : "main";
            query       = root.GetProperty("query").GetString()            ?? "";
            sessionName = root.TryGetProperty("sessionName", out var sn)  ? sn.GetString() ?? "main"   : "main";
            timeoutSeconds = root.TryGetProperty("timeoutSeconds", out var ts) ? ts.GetInt32() : 120;
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"agentId\":\"...\",\"query\":\"...\"}");
        }

        if (string.IsNullOrWhiteSpace(query))
            return Result<string, string>.Failure("query must not be empty.");

        // Use the existing gateway URL from SharedState if available, otherwise auto-detect
        var gatewayWsUrl = OpenClawSharedState.SharedClient?.IsConnected == true
            ? "ws://127.0.0.1:18789"  // Gateway is local; SDK adds /gateway path as needed
            : null;

        var config = new ClientConfig
        {
            GatewayWsUrl   = gatewayWsUrl,
            Mode           = GatewayMode.Auto,
            TimeoutSeconds = timeoutSeconds,
        };

        try
        {
            await using var client = await OpenClawClient.ConnectAsync(config, ct);
            var agent  = client.GetAgent(agentId, sessionName);
            var opts   = new ExecutionOptions { TimeoutSeconds = timeoutSeconds };
            var result = await agent.ExecuteAsync(query, opts, ct);

            if (!result.Success)
                return Result<string, string>.Failure($"Agent '{agentId}' error: {result.ErrorMessage}");

            var response = result.Content;
            if (result.LatencyMs > 0)
                response += $"\n\n[latency: {result.LatencyMs}ms | tokens: {result.TokenUsage.Total}]";

            return Result<string, string>.Success(response);
        }
        catch (ConfigurationException ex)
        {
            return Result<string, string>.Failure($"OpenClaw not available: {ex.Message}");
        }
        catch (GatewayException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (OperationCanceledException) { throw; }
    }
}
