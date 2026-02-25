using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.Affect;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.CLI.Subsystems;

/// <summary>
/// Manages skills, personality, MeTTa reasoning, neural memory, thought persistence,
/// conversation memory, and self-persistence.
/// </summary>
public interface IMemorySubsystem : IAgentSubsystem
{
    ISkillRegistry? Skills { get; }
    PersonalityEngine? PersonalityEngine { get; }
    PersonalityProfile? Personality { get; }
    IValenceMonitor? ValenceMonitor { get; }
    IMeTTaEngine? MeTTaEngine { get; }
    ThoughtPersistenceService? ThoughtPersistence { get; }
    List<InnerThought> PersistentThoughts { get; }
    string? LastThoughtContent { get; set; }
    QdrantNeuralMemory? NeuralMemory { get; }
    List<string> ConversationHistory { get; }

    // Persistent conversation memory (cross-session recall)
    PersistentConversationMemory? ConversationMemory { get; }

    // Self-persistence (mind state storage in Qdrant)
    SelfPersistence? SelfPersistence { get; }
}