using System.Text;

namespace Ouroboros.Application.Services;

/// <summary>
/// Complete snapshot of the autonomous mind's state.
/// </summary>
public class MindStateSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string PersonaName { get; set; } = "Ouroboros";
    public int ThoughtCount { get; set; }
    public List<string> LearnedFacts { get; set; } = [];
    public List<string> Interests { get; set; } = [];
    public List<Thought> RecentThoughts { get; set; } = [];
    public EmotionalState CurrentEmotion { get; set; } = new();

    public string ToSummaryText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Mind state of {PersonaName} at {Timestamp:g}");
        sb.AppendLine($"Thoughts: {ThoughtCount}, Facts learned: {LearnedFacts.Count}, Interests: {Interests.Count}");
        sb.AppendLine($"Emotional state: {CurrentEmotion.DominantEmotion} (valence={CurrentEmotion.Valence:F2}, arousal={CurrentEmotion.Arousal:F2})");

        if (Interests.Count > 0)
        {
            sb.AppendLine($"Interests: {string.Join(", ", Interests.Take(5))}");
        }

        if (LearnedFacts.Count > 0)
        {
            sb.AppendLine("Recent discoveries:");
            foreach (var fact in LearnedFacts.TakeLast(3))
            {
                sb.AppendLine($"  - {fact}");
            }
        }

        return sb.ToString();
    }
}