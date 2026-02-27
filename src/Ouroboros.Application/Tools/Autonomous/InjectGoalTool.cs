// <copyright file="InjectGoalTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Domain.Autonomous;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Injects a goal for autonomous pursuit.
/// </summary>
public class InjectGoalTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public InjectGoalTool(IAutonomousToolContext context) => _ctx = context;
    public InjectGoalTool() : this(AutonomousTools.DefaultContext) { }

    /// <inheritdoc/>
    public string Name => "set_autonomous_goal";

    /// <inheritdoc/>
    public string Description => "Give me a goal to pursue autonomously. Input JSON: {\"goal\": \"description\", \"priority\": \"Low|Normal|High|Critical\"}";

    /// <inheritdoc/>
    public string? JsonSchema => """{"type":"object","properties":{"goal":{"type":"string"},"priority":{"type":"string"}},"required":["goal"]}""";

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (_ctx.Coordinator == null)
            return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(input);
            var goal = args.GetProperty("goal").GetString() ?? "";
            var priorityStr = args.TryGetProperty("priority", out var p) ? p.GetString() ?? "Normal" : "Normal";

            if (!Enum.TryParse<IntentionPriority>(priorityStr, true, out var priority))
                priority = IntentionPriority.Normal;

            _ctx.Coordinator.InjectGoal(goal, priority);

            return Task.FromResult(Result<string, string>.Success(
                $"\ud83c\udfaf Goal injected: {goal}\n" +
                $"I will propose actions to work towards this goal."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Failed to set goal: {ex.Message}"));
        }
    }
}
