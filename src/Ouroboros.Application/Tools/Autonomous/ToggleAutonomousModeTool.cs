// <copyright file="ToggleAutonomousModeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

/// <summary>
/// Toggles autonomous mode.
/// </summary>
public class ToggleAutonomousModeTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public ToggleAutonomousModeTool(IAutonomousToolContext context) => _ctx = context;
    public ToggleAutonomousModeTool() : this(AutonomousTools.DefaultContext) { }

    /// <inheritdoc/>
    public string Name => "toggle_autonomous";

    /// <inheritdoc/>
    public string Description => "Start or stop my autonomous mode. Input: 'start' or 'stop'.";

    /// <inheritdoc/>
    public string? JsonSchema => null;

    /// <inheritdoc/>
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (_ctx.Coordinator == null)
            return Result<string, string>.Failure("Autonomous coordinator not initialized.");

        var action = input.Trim().ToLowerInvariant();

        if (action == "start")
        {
            if (_ctx.Coordinator.IsActive)
                return Result<string, string>.Success("Autonomous mode is already active.");

            _ctx.Coordinator.Start();
            return Result<string, string>.Success("\ud83d\udfe2 Autonomous mode started. I will now propose actions for your approval.");
        }
        else if (action == "stop")
        {
            if (!_ctx.Coordinator.IsActive)
                return Result<string, string>.Success("Autonomous mode is already stopped.");

            await _ctx.Coordinator.StopAsync();
            return Result<string, string>.Success("\ud83d\udd34 Autonomous mode stopped. I will wait for your instructions.");
        }

        return Result<string, string>.Failure("Invalid action. Use 'start' or 'stop'.");
    }
}
