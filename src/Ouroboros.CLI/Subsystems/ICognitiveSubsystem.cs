using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Application.Tools;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Learning;
using Ouroboros.Pipeline.Metacognition;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.CLI.Subsystems;

/// <summary>
/// Manages consciousness simulation and AGI cognitive subsystems:
/// learning, metacognition, council debate, world model, and agent coordination.
/// </summary>
public interface ICognitiveSubsystem : IAgentSubsystem
{
    // Consciousness
    ImmersivePersona? ImmersivePersona { get; }

    // Continuous Learning
    ContinuouslyLearningAgent? LearningAgent { get; }
    AdaptiveMetaLearner? MetaLearner { get; }
    ExperienceBuffer? ExperienceBuffer { get; }

    // Metacognition
    RealtimeCognitiveMonitor? CognitiveMonitor { get; }
    BayesianSelfAssessor? SelfAssessor { get; }
    CognitiveIntrospector? Introspector { get; }

    // Council & Coordination
    CouncilOrchestrator? CouncilOrchestrator { get; }
    AgentCoordinator? AgentCoordinator { get; }

    // World Model
    WorldState? WorldState { get; }

    // Distinction Learning
    IDistinctionLearner? DistinctionLearner { get; }
    ConsciousnessDream? Dream { get; }
    DistinctionState CurrentDistinctionState { get; set; }

    // Interconnected Learning (tool-skill bridging)
    InterconnectedLearner? InterconnectedLearner { get; }
}