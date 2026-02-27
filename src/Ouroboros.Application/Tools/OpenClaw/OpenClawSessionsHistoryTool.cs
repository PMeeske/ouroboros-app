// <copyright file="OpenClawSessionsHistoryTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Get message history for an OpenClaw session.
/// </summary>
public sealed class OpenClawSessionsHistoryTool : ITool
{
    public string Name => "openclaw_sessions_history";
    public string Description => "Get message history for an OpenClaw session.";
    public string? JsonSchema => """{"type":"object","properties":{"sessionId":{"type":"string","description":"Session ID to retrieve history for"},"limit":{"type":"integer","description":"Maximum messages to return (default: 50)"}},"required":["sessionId"]}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;
            var sessionId = root.GetProperty("sessionId").GetString() ?? "";
            var limit = root.TryGetProperty("limit", out var l) ? l.GetInt32() : 50;

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("sessions.history", new { sessionId, limit }, ct);
            return Result<string, string>.Success(result.ToString());
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"sessionId\":\"...\", \"limit\":50}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to get session history: {ex.Message}");
        }
    }
}
