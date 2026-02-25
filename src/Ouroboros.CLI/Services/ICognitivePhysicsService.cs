using Ouroboros.Abstractions.Monads;
using Ouroboros.Core.CognitivePhysics;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for executing Cognitive Physics Engine operations from the CLI.
/// Provides ZeroShift transitions, superposition branching, chaos injection,
/// and evolutionary adaptation as first-class CLI capabilities.
/// </summary>
public interface ICognitivePhysicsService
{
    /// <summary>
    /// Performs a ZeroShift context transition from the current focus to a target domain.
    /// </summary>
    /// <param name="initialFocus">The starting conceptual domain.</param>
    /// <param name="target">The target conceptual domain to shift to.</param>
    /// <param name="resources">Initial resource budget (default: 100).</param>
    /// <returns>Result containing the transitioned state or failure reason.</returns>
    Task<Result<CognitiveState>> ShiftAsync(string initialFocus, string target, double resources = 100.0);

    /// <summary>
    /// Executes a multi-target reasoning trajectory through ordered conceptual domains.
    /// </summary>
    /// <param name="initialFocus">The starting conceptual domain.</param>
    /// <param name="targets">Ordered list of target domains to traverse.</param>
    /// <param name="resources">Initial resource budget (default: 100).</param>
    /// <returns>Result containing the final cognitive state or failure reason.</returns>
    Task<Result<CognitiveState>> ExecuteTrajectoryAsync(string initialFocus, IReadOnlyList<string> targets, double resources = 100.0);

    /// <summary>
    /// Enters superposition, branching into multiple target contexts simultaneously.
    /// </summary>
    /// <param name="initialFocus">The starting conceptual domain.</param>
    /// <param name="targets">Contexts to branch into.</param>
    /// <param name="resources">Initial resource budget (default: 100).</param>
    /// <returns>List of weighted cognitive branches.</returns>
    Task<ImmutableList<CognitiveBranch>> EntangleAsync(string initialFocus, IReadOnlyList<string> targets, double resources = 100.0);

    /// <summary>
    /// Collapses superposition branches back to the best single state using coherence scoring.
    /// </summary>
    /// <param name="origin">The original focus before superposition.</param>
    /// <param name="branches">The competing branches to collapse.</param>
    /// <returns>The collapsed best state, or None if all branches fail.</returns>
    Task<Option<CognitiveState>> CollapseAsync(string origin, ImmutableList<CognitiveBranch> branches);

    /// <summary>
    /// Injects controlled chaos into a cognitive state for creative exploration.
    /// </summary>
    /// <param name="initialFocus">The current conceptual domain.</param>
    /// <param name="resources">Initial resource budget (default: 100).</param>
    /// <returns>Result containing the chaotic state or failure reason.</returns>
    Result<CognitiveState> InjectChaos(string initialFocus, double resources = 100.0);
}
