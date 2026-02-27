// <copyright file="InnerDialogEngine.Persistence.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

/// <summary>
/// Dialog persistence and retrieval: session management, result building, trait calculation.
/// </summary>
public sealed partial class InnerDialogEngine
{
    /// <summary>
    /// Persists thoughts to the storage backend if enabled.
    /// </summary>
    private async Task PersistThoughtsAsync(List<InnerThought> thoughts, string? topic, CancellationToken ct)
    {
        if (_persistenceService == null || thoughts.Count == 0) return;

        try
        {
            await _persistenceService.SaveManyAsync(thoughts, topic, ct);
        }
        catch (Exception ex)
        {
            // Log but don't fail - persistence is non-critical
            Console.WriteLine($"[ThoughtPersistence] Failed to save thoughts: {ex.Message}");
        }
    }

    private static string ExtractTopic(string input)
    {
        // Simple topic extraction - could be enhanced with NLP
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "what", "how", "why", "can", "could", "would", "should", "i", "you", "me", "we", "they" };

        var significantWords = words
            .Where(w => w.Length > 3 && !stopWords.Contains(w.ToLower()))
            .Take(3)
            .ToArray();

        return significantWords.Length > 0 ? string.Join(" ", significantWords) : "this topic";
    }

    private static string FormulateDecision(InnerDialogSession session)
    {
        // Synthesize decision from all thoughts
        var hasEthicalConcern = session.Thoughts.Any(t => t.Type == InnerThoughtType.Ethical);
        var hasCreativeAngle = session.Thoughts.Any(t => t.Type == InnerThoughtType.Creative && t.Confidence > 0.6);
        var emotional = session.EmotionalTone ?? "balanced";

        var decision = $"respond with {emotional} engagement";

        if (hasCreativeAngle)
            decision += ", incorporating creative elements";
        if (hasEthicalConcern)
            decision += ", while being mindful of ethical considerations";

        return decision;
    }

    private static Dictionary<string, double> CalculateTraitInfluences(InnerDialogSession session, PersonalityProfile? profile)
    {
        var influences = new Dictionary<string, double>();

        foreach (var thought in session.Thoughts)
        {
            if (thought.TriggeringTrait != null)
            {
                if (!influences.ContainsKey(thought.TriggeringTrait))
                    influences[thought.TriggeringTrait] = 0;
                influences[thought.TriggeringTrait] += thought.Confidence * 0.2;
            }
        }

        // Normalize to 0-1
        var max = influences.Values.DefaultIfEmpty(1).Max();
        if (max > 0)
        {
            foreach (var key in influences.Keys.ToList())
            {
                influences[key] = Math.Min(1.0, influences[key] / max);
            }
        }

        return influences;
    }

    private void StoreSession(InnerDialogSession session, string personaName)
    {
        _sessionHistory.AddOrUpdate(
            personaName,
            _ => new List<InnerDialogSession> { session },
            (_, list) =>
            {
                list.Add(session);
                // Keep last 50 sessions
                while (list.Count > 50)
                    list.RemoveAt(0);
                return list;
            });
    }

    private InnerDialogResult BuildResult(InnerDialogSession session, PersonalityProfile? profile, DetectedMood? userMood)
    {
        // Determine suggested response tone
        var tone = session.EmotionalTone ?? "balanced";
        if (userMood?.Frustration > 0.4)
            tone = "supportive";

        // Extract key insights
        var insights = session.Thoughts
            .Where(t => t.Confidence > 0.7)
            .Select(t => $"{t.Type}: {TruncateForInsight(t.Content)}")
            .Take(5)
            .ToArray();

        // Determine if we should ask a proactive question
        string? proactiveQuestion = null;
        if (profile?.Traits.TryGetValue("curious", out var curious) == true && curious.Intensity > 0.6)
        {
            var strategicThought = session.Thoughts.FirstOrDefault(t => t.Type == InnerThoughtType.Strategic);
            if (strategicThought != null && !strategicThought.Content.Contains("concise"))
            {
                proactiveQuestion = GenerateProactiveQuestion(session.Topic);
            }
        }

        // Build response guidance
        var guidance = new Dictionary<string, object>
        {
            ["tone"] = tone,
            ["confidence"] = session.OverallConfidence,
            ["include_creative"] = session.Thoughts.Any(t => t.Type == InnerThoughtType.Creative && t.Confidence > 0.6),
            ["be_concise"] = session.Thoughts.Any(t => t.Content.Contains("concise")),
            ["acknowledge_feelings"] = userMood?.Frustration > 0.3 || userMood?.Positivity < 0.4
        };

        return new InnerDialogResult(session, tone, insights, proactiveQuestion, guidance);
    }

    private static string TruncateForInsight(string content)
    {
        if (content.Length <= 50) return content;
        return content[..47] + "...";
    }

    private string? GenerateProactiveQuestion(string? topic)
    {
        if (string.IsNullOrEmpty(topic)) return null;

        var questions = new[]
        {
            $"What aspect of {topic} would you like to explore further?",
            $"Is there a specific challenge with {topic} you're facing?",
            $"What got you interested in {topic}?",
            $"How does {topic} fit into what you're working on?"
        };

        return questions[_random.Next(questions.Length)];
    }
}
