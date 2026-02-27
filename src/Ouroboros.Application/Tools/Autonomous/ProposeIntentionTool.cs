// <copyright file="ProposeIntentionTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Domain.Autonomous;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Proposes a new intention.
/// </summary>
public class ProposeIntentionTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public ProposeIntentionTool(IAutonomousToolContext context) => _ctx = context;
    public ProposeIntentionTool() : this(AutonomousTools.DefaultContext) { }

    /// <inheritdoc/>
    public string Name => "propose_intention";

    /// <inheritdoc/>
    public string Description => """
        Propose a new intention for user approval. Input JSON:
        {
            "title": "Short title",
            "description": "What I want to do",
            "rationale": "Why this is beneficial",
            "category": "SelfReflection|CodeModification|Learning|UserCommunication|MemoryManagement|GoalPursuit",
            "priority": "Low|Normal|High|Critical"
        }
        """;

    /// <inheritdoc/>
    public string? JsonSchema => """{"type":"object","properties":{"title":{"type":"string"},"description":{"type":"string"},"rationale":{"type":"string"},"category":{"type":"string"},"priority":{"type":"string"}},"required":["title","description","rationale","category"]}""";

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (_ctx.Coordinator == null)
            return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(input);
            var title = args.GetProperty("title").GetString() ?? "";
            var description = args.GetProperty("description").GetString() ?? "";
            var rationale = args.GetProperty("rationale").GetString() ?? "";
            var categoryStr = args.GetProperty("category").GetString() ?? "SelfReflection";
            var priorityStr = args.TryGetProperty("priority", out var p) ? p.GetString() ?? "Normal" : "Normal";

            if (!Enum.TryParse<IntentionCategory>(categoryStr, true, out var category))
                category = IntentionCategory.SelfReflection;

            if (!Enum.TryParse<IntentionPriority>(priorityStr, true, out var priority))
                priority = IntentionPriority.Normal;

            var intention = _ctx.Coordinator.IntentionBus.ProposeIntention(
                title, description, rationale, category, "self", null, priority);

            return Task.FromResult(Result<string, string>.Success(
                $"\ud83d\udcdd Intention proposed: **{title}**\n" +
                $"ID: `{intention.Id.ToString()[..8]}`\n" +
                $"Awaiting user approval."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Failed to propose: {ex.Message}"));
        }
    }
}
