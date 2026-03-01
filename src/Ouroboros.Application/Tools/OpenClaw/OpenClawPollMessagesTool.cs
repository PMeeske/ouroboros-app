// <copyright file="OpenClawPollMessagesTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Wait for new incoming messages with a timeout. Returns when messages arrive or timeout expires.
/// </summary>
public sealed class OpenClawPollMessagesTool : ITool
{
    public string Name => "openclaw_poll_messages";
    public string Description => "Wait for new incoming messages with a timeout. Returns when messages arrive or timeout expires.";
    public string? JsonSchema => """{"type":"object","properties":{"channel":{"type":"string","description":"Optional channel filter"},"timeout":{"type":"integer","description":"Timeout in seconds (default: 30, max: 120)"}}}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();

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

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("messages.poll", p, ct);
            return Result<string, string>.Success(result.ToString());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"channel\":\"...\", \"timeout\":30}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to poll messages: {ex.Message}");
        }
    }
}
