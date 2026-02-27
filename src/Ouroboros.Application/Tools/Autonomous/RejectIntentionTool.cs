// <copyright file="RejectIntentionTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Rejects a pending intention.
/// </summary>
public class RejectIntentionTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public RejectIntentionTool(IAutonomousToolContext context) => _ctx = context;
    public RejectIntentionTool() : this(AutonomousTools.DefaultContext) { }

    /// <inheritdoc/>
    public string Name => "reject_my_intention";

    /// <inheritdoc/>
    public string Description => "Reject one of my pending intentions. Input JSON: {\"id\": \"partial_id\", \"reason\": \"optional\"}";

    /// <inheritdoc/>
    public string? JsonSchema => """{"type":"object","properties":{"id":{"type":"string"},"reason":{"type":"string"}},"required":["id"]}""";

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (_ctx.Coordinator == null)
            return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(input);
            var id = args.GetProperty("id").GetString() ?? "";
            var reason = args.TryGetProperty("reason", out var r) ? r.GetString() : null;

            var success = _ctx.Coordinator.IntentionBus.RejectIntentionByPartialId(id, reason);

            return Task.FromResult(success
                ? Result<string, string>.Success($"\u274c Intention `{id}` rejected.")
                : Result<string, string>.Failure($"Could not find pending intention with ID starting with: {id}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Failed to reject: {ex.Message}"));
        }
    }
}
