// <copyright file="OpenClawAgentChatTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Allows Iaret to send a query to a named OpenClaw gateway agent and receive its response.
/// Uses the shared <see cref="OpenClawSharedState.SharedClient"/> (already authenticated)
/// and subscribes to <c>OnPushMessage</c> push events to collect the streaming response.
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
            agentId        = root.TryGetProperty("agentId",        out var aid) ? aid.GetString() ?? "main" : "main";
            query          = root.GetProperty("query").GetString() ?? "";
            sessionName    = root.TryGetProperty("sessionName",    out var sn)  ? sn.GetString()  ?? "main" : "main";
            timeoutSeconds = root.TryGetProperty("timeoutSeconds", out var ts)  ? ts.GetInt32()             : 120;
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"agentId\":\"...\",\"query\":\"...\"}");
        }

        if (string.IsNullOrWhiteSpace(query))
            return Result<string, string>.Failure("query must not be empty.");

        var client = OpenClawSharedState.SharedClient;
        if (client == null || !client.IsConnected)
            return OpenClawSharedState.NotConnected();

        var sessionKey      = $"agent:{agentId}:{sessionName}";
        var idempotencyKey  = Guid.NewGuid().ToString("N");
        var content         = new StringBuilder();
        string? runId       = null;
        var done            = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(string eventName, JsonElement payload)
        {
            if (eventName != "agent") return;

            // Once we know the runId, filter to only our run
            if (runId != null
                && payload.TryGetProperty("runId", out var ridProp)
                && ridProp.GetString() is { } payloadRunId
                && payloadRunId != runId)
                return;

            var stream = payload.TryGetProperty("stream", out var sProp) ? sProp.GetString() : null;

            if (stream == "assistant"
                && payload.TryGetProperty("data", out var data)
                && data.TryGetProperty("delta", out var delta))
            {
                content.Append(delta.GetString());
            }
            else if (stream == "lifecycle"
                && payload.TryGetProperty("data", out var ld))
            {
                var phase = ld.TryGetProperty("phase", out var pProp) ? pProp.GetString() : null;

                if (phase == "end")
                {
                    // Some gateways send the full content in lifecycle.end instead of deltas
                    if (content.Length == 0 && ld.TryGetProperty("content", out var lc))
                        content.Append(lc.GetString());
                    done.TrySetResult(null);
                }
                else if (phase == "error")
                {
                    var errMsg = ld.TryGetProperty("error", out var em) ? em.GetString() : "Agent error";
                    done.TrySetResult(errMsg ?? "Agent error");
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
                    agentId,
                    message        = query,
                    idempotencyKey,
                    timeoutMs      = timeoutSeconds * 1000,
                },
                ct);

            // Extract runId from the response (payload.runId or runId at root)
            if (sendResult.TryGetProperty("payload", out var pl) && pl.TryGetProperty("runId", out var r))
                runId = r.GetString();
            else if (sendResult.TryGetProperty("runId", out var r2))
                runId = r2.GetString();

            // Wait for lifecycle end/error or timeout
            using var timeoutCts  = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts   = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            using var reg         = linkedCts.Token.Register(() => done.TrySetCanceled(linkedCts.Token));

            var error = await done.Task;

            if (error != null)
                return Result<string, string>.Failure($"Agent '{agentId}' error: {error}");

            var response = content.ToString();
            if (string.IsNullOrWhiteSpace(response))
                return Result<string, string>.Failure($"Agent '{agentId}' returned an empty response.");

            return Result<string, string>.Success(response);
        }
        catch (OpenClaw.OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (OperationCanceledException) { throw; }
        finally
        {
            client.OnPushMessage -= Handler;
        }
    }
}
