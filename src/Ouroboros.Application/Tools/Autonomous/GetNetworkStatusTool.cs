// <copyright file="GetNetworkStatusTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

/// <summary>
/// Gets neural network status.
/// </summary>
public class GetNetworkStatusTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public GetNetworkStatusTool(IAutonomousToolContext context) => _ctx = context;
    public GetNetworkStatusTool() : this(AutonomousTools.DefaultContext) { }

    /// <inheritdoc/>
    public string Name => "neural_network_status";

    /// <inheritdoc/>
    public string Description => "Get the status of my internal neural network including all active neurons.";

    /// <inheritdoc/>
    public string? JsonSchema => null;

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (_ctx.Coordinator == null)
            return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

        return Task.FromResult(Result<string, string>.Success(
            _ctx.Coordinator.Network.GetNetworkState()));
    }
}
