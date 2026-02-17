using System.Text;

namespace Ouroboros.Application.Personality;

/// <summary>
/// A complete inner dialog session - the AI's internal monologue before responding.
/// </summary>
public sealed record InnerDialogSession(
    Guid Id,
    string UserInput,
    string? Topic,
    List<InnerThought> Thoughts,
    string? FinalDecision,
    Dictionary<string, double> TraitInfluences,
    string? EmotionalTone,
    double OverallConfidence,
    TimeSpan ProcessingTime,
    DateTime StartTime)
{
    /// <summary>Creates a new dialog session.</summary>
    public static InnerDialogSession Start(string userInput, string? topic = null) => new(
        Id: Guid.NewGuid(),
        UserInput: userInput,
        Topic: topic,
        Thoughts: new List<InnerThought>(),
        FinalDecision: null,
        TraitInfluences: new Dictionary<string, double>(),
        EmotionalTone: null,
        OverallConfidence: 0.5,
        ProcessingTime: TimeSpan.Zero,
        StartTime: DateTime.UtcNow);

    /// <summary>Adds a thought to the session.</summary>
    public InnerDialogSession AddThought(InnerThought thought)
    {
        List<InnerThought> thoughts = new(Thoughts) { thought };
        return this with { Thoughts = thoughts };
    }

    /// <summary>Gets the full inner monologue as text.</summary>
    public string GetMonologue()
    {
        StringBuilder sb = new();
        sb.AppendLine($"[Inner Dialog - {Topic ?? "general"}]");
        sb.AppendLine();

        foreach (InnerThought thought in Thoughts)
        {
            string prefix = thought.Type switch
            {
                InnerThoughtType.Observation => "👁️ OBSERVING:",
                InnerThoughtType.Emotional => "💭 FEELING:",
                InnerThoughtType.Analytical => "🔍 ANALYZING:",
                InnerThoughtType.SelfReflection => "🪞 REFLECTING:",
                InnerThoughtType.MemoryRecall => "📚 REMEMBERING:",
                InnerThoughtType.Strategic => "🎯 PLANNING:",
                InnerThoughtType.Ethical => "⚖️ CONSIDERING:",
                InnerThoughtType.Creative => "💡 IMAGINING:",
                InnerThoughtType.Synthesis => "🔗 CONNECTING:",
                InnerThoughtType.Decision => "✅ DECIDING:",
                _ => "💬"
            };

            sb.AppendLine($"{prefix} {thought.Content}");
            if (thought.TriggeringTrait != null)
                sb.AppendLine($"   (via {thought.TriggeringTrait} trait, confidence: {thought.Confidence:P0})");
            sb.AppendLine();
        }

        if (FinalDecision != null)
        {
            sb.AppendLine($"[Final Decision: {FinalDecision}]");
        }

        return sb.ToString();
    }

    /// <summary>Completes the session with a final decision.</summary>
    public InnerDialogSession Complete(string decision)
    {
        TimeSpan elapsed = DateTime.UtcNow - StartTime;
        double avgConfidence = Thoughts.Count > 0 ? Thoughts.Average(t => t.Confidence) : 0.5;
        return this with
        {
            FinalDecision = decision,
            ProcessingTime = elapsed,
            OverallConfidence = avgConfidence
        };
    }
}