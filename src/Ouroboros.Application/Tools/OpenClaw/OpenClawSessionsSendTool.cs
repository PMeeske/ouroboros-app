// <copyright file="OpenClawSessionsSendTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Inject a message into an existing OpenClaw session.
/// </summary>
public sealed class OpenClawSessionsSendTool : ITool
{
    public string Name => "openclaw_sessions_send";
    public string Description => "Inject a message into an existing OpenClaw session.";
    public string? JsonSchema => """{"type":"object","properties":{"sessionId":{"type":"string","description":"Target session ID"},"message":{"type":"string","description":"Message to inject"}},"required":["sessionId","message"]}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();
        if (OpenClawSharedState.SharedPolicy == null)
            return OpenClawSharedState.PolicyNotInitialized();

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;
            var sessionId = root.GetProperty("sessionId").GetString() ?? "";
            var message = root.GetProperty("message").GetString() ?? "";

            // Scan session messages for sensitive data (same as send_message)
            var verdict = OpenClawSharedState.SharedPolicy.ValidateSendMessage("session", sessionId, message);
            if (!verdict.IsAllowed)
                return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("sessions.send", new { sessionId, message }, ct);
            return Result<string, string>.Success($"Message injected into session {sessionId}. Response: {result}");
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"sessionId\":\"...\", \"message\":\"...\"}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to send to session: {ex.Message}");
        }
    }
}
