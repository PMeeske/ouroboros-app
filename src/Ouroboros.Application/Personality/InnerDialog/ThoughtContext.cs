namespace Ouroboros.Application.Personality;

/// <summary>
/// Context passed to thought providers for generating thoughts.
/// </summary>
public sealed record ThoughtContext(
    string? UserInput,
    string? Topic,
    PersonalityProfile? Profile,
    SelfAwareness? SelfAwareness,
    DetectedMood? UserMood,
    List<ConversationMemory>? RelevantMemories,
    List<InnerThought> PreviousThoughts,
    ConsciousnessState? ConsciousnessState,
    Dictionary<string, object> CustomContext)
{
    /// <summary>Creates an empty context for autonomous thinking.</summary>
    public static ThoughtContext ForAutonomous(PersonalityProfile? profile, SelfAwareness? self) =>
        new(null, null, profile, self, null, null, new(), null, new());

    /// <summary>Creates a context from user input.</summary>
    public static ThoughtContext FromInput(string input, string? topic, PersonalityProfile? profile) =>
        new(input, topic, profile, null, null, null, new(), null, new());
}