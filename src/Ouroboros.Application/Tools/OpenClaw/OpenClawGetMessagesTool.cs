// <copyright file="OpenClawGetMessagesTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Get recent incoming messages from OpenClaw channels.
/// </summary>
public sealed class OpenClawGetMessagesTool : ITool
{
    public string Name => "openclaw_get_messages";
    public string Description => "Get recent incoming messages from OpenClaw channels.";
    public string? JsonSchema => """{"type":"object","properties":{"channel":{"type":"string","description":"Optional channel filter (e.g. 'whatsapp', 'telegram')"},"limit":{"type":"integer","description":"Maximum messages (default: 20)"}}}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();

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

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("messages.list", p, ct);
            return Result<string, string>.Success(result.ToString());
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"channel\":\"...\", \"limit\":20}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to get messages: {ex.Message}");
        }
    }
}
