// <copyright file="OpenClawSessionsSpawnTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Spawn a new OpenClaw agent session with a prompt.
/// </summary>
public sealed class OpenClawSessionsSpawnTool : ITool
{
    public string Name => "openclaw_sessions_spawn";
    public string Description => "Spawn a new OpenClaw agent session with a prompt.";
    public string? JsonSchema => """{"type":"object","properties":{"prompt":{"type":"string","description":"Initial prompt for the new session"},"channel":{"type":"string","description":"Optional channel to bind the session to"}},"required":["prompt"]}""";

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
            var prompt = root.GetProperty("prompt").GetString() ?? "";
            var channel = root.TryGetProperty("channel", out var ch) ? ch.GetString() : null;

            var p = channel != null
                ? (object)new { prompt, channel }
                : new { prompt };

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("sessions.spawn", p, ct);
            return Result<string, string>.Success($"Session spawned. Response: {result}");
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON. Expected: {\"prompt\":\"...\"}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to spawn session: {ex.Message}");
        }
    }
}
