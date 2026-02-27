// <copyright file="GetAutonomousStatusTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

/// <summary>
/// Gets the current autonomous status.
/// </summary>
public class GetAutonomousStatusTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public GetAutonomousStatusTool(IAutonomousToolContext context) => _ctx = context;
    public GetAutonomousStatusTool() : this(AutonomousTools.DefaultContext) { }

    /// <inheritdoc/>
    public string Name => "autonomous_status";

    /// <inheritdoc/>
    public string Description => "Get my current autonomous mode status including pending intentions, neural network state, and configuration.";

    /// <inheritdoc/>
    public string? JsonSchema => null;

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (_ctx.Coordinator == null)
            return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

        return Task.FromResult(Result<string, string>.Success(_ctx.Coordinator.GetStatus()));
    }
}
