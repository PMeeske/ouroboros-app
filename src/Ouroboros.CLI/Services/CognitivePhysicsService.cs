using Microsoft.Extensions.Logging;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Core.CognitivePhysics;
using Ouroboros.Domain;

namespace Ouroboros.CLI.Services;

/// <summary>
/// CLI service wrapping the foundation CognitivePhysicsEngine.
/// Provides ZeroShift transitions, superposition branching, chaos injection,
/// and evolutionary adaptation as first-class CLI capabilities.
/// </summary>
public sealed class CognitivePhysicsService : ICognitivePhysicsService
{
    private readonly CognitivePhysicsEngine _engine;
    private readonly ILogger<CognitivePhysicsService> _logger;

    public CognitivePhysicsService(
        IEmbeddingModel embeddingModel,
        IEthicsGate ethicsGate,
        ILogger<CognitivePhysicsService> logger,
        CognitivePhysicsConfig? config = null)
    {
        _engine = new CognitivePhysicsEngine(embeddingModel, ethicsGate, config);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<CognitiveState>> ShiftAsync(
        string initialFocus, string target, double resources = 100.0)
    {
        _logger.LogInformation("ZeroShift: {From} → {To} (resources={Resources})",
            initialFocus, target, resources);

        var state = CognitiveState.Create(initialFocus, resources);
        var step = _engine.ShiftStep(target);
        var result = await step(state);

        if (result.IsSuccess)
            _logger.LogInformation("Shift succeeded. Focus={Focus}, Resources={Res:F1}, Compression={Comp:F3}",
                result.Value.Focus, result.Value.Resources, result.Value.Compression);
        else
            _logger.LogWarning("Shift failed: {Error}", result.Error);

        return result;
    }

    /// <inheritdoc/>
    public async Task<Result<CognitiveState>> ExecuteTrajectoryAsync(
        string initialFocus, IReadOnlyList<string> targets, double resources = 100.0)
    {
        _logger.LogInformation("Trajectory: {From} → [{Targets}] (resources={Resources})",
            initialFocus, string.Join(" → ", targets), resources);

        var state = CognitiveState.Create(initialFocus, resources);
        var result = await _engine.ExecuteTrajectoryAsync(state, targets);

        if (result.IsSuccess)
            _logger.LogInformation("Trajectory completed. Final focus={Focus}, Resources={Res:F1}",
                result.Value.Focus, result.Value.Resources);
        else
            _logger.LogWarning("Trajectory failed: {Error}", result.Error);

        return result;
    }

    /// <inheritdoc/>
    public async Task<ImmutableList<CognitiveBranch>> EntangleAsync(
        string initialFocus, IReadOnlyList<string> targets, double resources = 100.0)
    {
        _logger.LogInformation("Entangle: {From} → [{Targets}]",
            initialFocus, string.Join(", ", targets));

        var state = CognitiveState.Create(initialFocus, resources);
        var step = _engine.EntangleStep(targets);
        var branches = await step(state);

        _logger.LogInformation("Entangled into {Count} branches", branches.Count);
        return branches;
    }

    /// <inheritdoc/>
    public async Task<Option<CognitiveState>> CollapseAsync(
        string origin, ImmutableList<CognitiveBranch> branches)
    {
        _logger.LogInformation("Collapse: origin={Origin}, branches={Count}",
            origin, branches.Count);

        var step = _engine.CollapseStep(origin);
        var result = await step(branches);

        _logger.LogInformation("Collapse result: {HasValue}",
            result.HasValue ? "converged" : "all branches failed");
        return result;
    }

    /// <inheritdoc/>
    public Result<CognitiveState> InjectChaos(string initialFocus, double resources = 100.0)
    {
        _logger.LogInformation("Chaos injection: focus={Focus}, resources={Resources}",
            initialFocus, resources);

        var state = CognitiveState.Create(initialFocus, resources);
        var result = _engine.Chaos.Inject(state);

        if (result.IsSuccess)
            _logger.LogInformation("Chaos injected. Focus={Focus}, Resources={Res:F1}",
                result.Value.Focus, result.Value.Resources);
        else
            _logger.LogWarning("Chaos injection failed: {Error}", result.Error);

        return result;
    }
}
