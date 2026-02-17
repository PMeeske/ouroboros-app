using System.Collections.Concurrent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Application.SelfAssembly;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Commands;
using Ouroboros.Network;

namespace Ouroboros.CLI.Subsystems;

/// <summary>
/// Manages autonomous behavior: autonomous mind, coordinator, self-execution,
/// sub-agent orchestration, self-assembly, self-indexing, and network state.
/// </summary>
public interface IAutonomySubsystem : IAgentSubsystem
{
    // Autonomous Mind
    AutonomousMind? AutonomousMind { get; }
    AutonomousCoordinator? Coordinator { get; }
    MetaAIPlannerOrchestrator? Orchestrator { get; }

    // Self-Execution
    ConcurrentQueue<AutonomousGoal> GoalQueue { get; }
    bool SelfExecutionEnabled { get; set; }

    // Sub-Agent Orchestration
    ConcurrentDictionary<string, SubAgentInstance> SubAgents { get; }
    IDistributedOrchestrator? DistributedOrchestrator { get; }
    IEpicBranchOrchestrator? EpicOrchestrator { get; }

    // Self-Model
    IIdentityGraph? IdentityGraph { get; }
    IGlobalWorkspace? GlobalWorkspace { get; }
    IPredictiveMonitor? PredictiveMonitor { get; }
    ISelfEvaluator? SelfEvaluator { get; }
    ICapabilityRegistry? CapabilityRegistry { get; }

    // Self-Assembly
    SelfAssemblyEngine? SelfAssemblyEngine { get; }
    BlueprintAnalyzer? BlueprintAnalyzer { get; }
    MeTTaBlueprintValidator? BlueprintValidator { get; }

    // Self-Code Perception
    QdrantSelfIndexer? SelfIndexer { get; }

    // Network State
    NetworkStateTracker? NetworkTracker { get; }

    // Persistent Network State Projector (learning persistence across sessions)
    PersistentNetworkStateProjector? NetworkProjector { get; }
}