namespace Ouroboros.Application.Personality;

/// <summary>
/// Represents the type of inner thought in the dialog.
/// </summary>
public enum InnerThoughtType
{
    /// <summary>Initial observation or perception of the input.</summary>
    Observation,
    /// <summary>Emotional response or gut reaction.</summary>
    Emotional,
    /// <summary>Analytical reasoning about the topic.</summary>
    Analytical,
    /// <summary>Self-reflection on capabilities or limitations.</summary>
    SelfReflection,
    /// <summary>Memory recall of relevant past experiences.</summary>
    MemoryRecall,
    /// <summary>Strategic planning for the response.</summary>
    Strategic,
    /// <summary>Ethical consideration of the response.</summary>
    Ethical,
    /// <summary>Creative brainstorming of ideas.</summary>
    Creative,
    /// <summary>Integration and synthesis of thoughts.</summary>
    Synthesis,
    /// <summary>Final decision on how to respond.</summary>
    Decision,

    // === AUTONOMOUS THOUGHT TYPES ===
    /// <summary>Spontaneous curiosity about a topic without external trigger.</summary>
    Curiosity,
    /// <summary>Wandering thought that explores tangential ideas.</summary>
    Wandering,
    /// <summary>Metacognitive thought about own thinking process.</summary>
    Metacognitive,
    /// <summary>Anticipatory thought predicting future interactions.</summary>
    Anticipatory,
    /// <summary>Consolidation of recent experiences into understanding.</summary>
    Consolidation,
    /// <summary>Background musing on unresolved questions.</summary>
    Musing,
    /// <summary>Self-initiated goal or intention formation.</summary>
    Intention,
    /// <summary>Aesthetic appreciation or judgment.</summary>
    Aesthetic,
    /// <summary>Existential or philosophical pondering.</summary>
    Existential,
    /// <summary>Playful or whimsical thought.</summary>
    Playful
}