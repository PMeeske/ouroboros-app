// <copyright file="ApproveIntentionTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Approves a pending intention.
/// </summary>
public class ApproveIntentionTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public ApproveIntentionTool(IAutonomousToolContext context) => _ctx = context;
    public ApproveIntentionTool() : this(AutonomousTools.DefaultContext) { }

    /// <inheritdoc/>
    public string Name => "approve_my_intention";

    /// <inheritdoc/>
    public string Description => "Approve one of my pending intentions so I can execute it. Input JSON: {\"id\": \"partial_id\", \"comment\": \"optional\"}";

    /// <inheritdoc/>
    public string? JsonSchema => """{"type":"object","properties":{"id":{"type":"string"},"comment":{"type":"string"}},"required":["id"]}""";

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (_ctx.Coordinator == null)
            return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(input);
            var id = args.GetProperty("id").GetString() ?? "";
            var comment = args.TryGetProperty("comment", out var c) ? c.GetString() : null;

            var success = _ctx.Coordinator.IntentionBus.ApproveIntentionByPartialId(id, comment);

            return Task.FromResult(success
                ? Result<string, string>.Success($"\u2705 Intention `{id}` approved and queued for execution.")
                : Result<string, string>.Failure($"Could not find pending intention with ID starting with: {id}"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Failed to approve: {ex.Message}"));
        }
    }
}
