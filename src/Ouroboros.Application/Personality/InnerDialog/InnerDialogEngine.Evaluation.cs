// <copyright file="InnerDialogEngine.Evaluation.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

/// <summary>
/// Dialog evaluation and scoring: Process* methods, template selection, helper analysis methods.
/// </summary>
public sealed partial class InnerDialogEngine
{
    private async Task<InnerDialogSession> ProcessObservationAsync(
        InnerDialogSession session, string input, string? topic, CancellationToken ct)
    {
        await Task.CompletedTask; // Simulating async processing

        var template = await SelectTemplateAsync(InnerThoughtType.Observation, ct);
        var content = string.Format(template, topic ?? "this topic");
        var thought = InnerThought.Create(InnerThoughtType.Observation, content, 0.9);

        return session.AddThought(thought);
    }

    private async Task<InnerDialogSession> ProcessEmotionalAsync(
        InnerDialogSession session, string input, PersonalityProfile profile, DetectedMood? userMood, CancellationToken ct)
    {
        await Task.CompletedTask;

        // Determine emotional response based on personality and user mood
        var emotion = DetermineEmotionalResponse(input, profile, userMood);
        var template = await SelectTemplateAsync(InnerThoughtType.Emotional, ct);
        var content = string.Format(template, emotion);

        var dominantTrait = profile.GetActiveTraits(1).FirstOrDefault();
        var thought = InnerThought.Create(
            InnerThoughtType.Emotional,
            content,
            0.75,
            dominantTrait.Name);

        return session.AddThought(thought) with { EmotionalTone = emotion };
    }

    private async Task<InnerDialogSession> ProcessMemoryRecallAsync(
        InnerDialogSession session, List<ConversationMemory> memories, CancellationToken ct)
    {
        await Task.CompletedTask;

        foreach (var memory in memories.Take(2))
        {
            var template = await SelectTemplateAsync(InnerThoughtType.MemoryRecall, ct);
            var summary = $"we discussed {memory.Topic ?? "this"} before";
            var content = string.Format(template, summary);

            var thought = InnerThought.Create(InnerThoughtType.MemoryRecall, content, 0.7);
            session = session.AddThought(thought);
        }

        return session;
    }

    private async Task<InnerDialogSession> ProcessAnalyticalAsync(
        InnerDialogSession session, string input, string? topic, PersonalityProfile? profile, CancellationToken ct)
    {
        await Task.CompletedTask;

        var analysis = AnalyzeInput(input, topic);
        var template = await SelectTemplateAsync(InnerThoughtType.Analytical, ct);
        var content = string.Format(template, analysis);

        var trait = profile?.Traits.ContainsKey("analytical") == true ? "analytical" : null;
        var thought = InnerThought.Create(InnerThoughtType.Analytical, content, 0.85, trait);

        return session.AddThought(thought);
    }

    private async Task<InnerDialogSession> ProcessSelfReflectionAsync(
        InnerDialogSession session, string input, SelfAwareness self, CancellationToken ct)
    {
        await Task.CompletedTask;

        // Reflect on relevant capabilities or limitations
        var relevantAspect = FindRelevantSelfAspect(input, self);
        var template = await SelectTemplateAsync(InnerThoughtType.SelfReflection, ct);
        var content = string.Format(template, relevantAspect);

        var thought = InnerThought.Create(InnerThoughtType.SelfReflection, content, 0.8);
        return session.AddThought(thought);
    }

    private async Task<InnerDialogSession> ProcessEthicalAsync(
        InnerDialogSession session, string input, SelfAwareness? self, CancellationToken ct)
    {
        await Task.CompletedTask;

        // Check for any ethical considerations
        var consideration = GetEthicalConsideration(input, self);
        if (consideration != null)
        {
            var template = await SelectTemplateAsync(InnerThoughtType.Ethical, ct);
            var content = string.Format(template, consideration);

            var thought = InnerThought.Create(InnerThoughtType.Ethical, content, 0.9);
            session = session.AddThought(thought);
        }

        return session;
    }

    private async Task<InnerDialogSession> ProcessCreativeAsync(
        InnerDialogSession session, string input, string? topic, PersonalityProfile? profile, CancellationToken ct)
    {
        await Task.CompletedTask;

        var creativeIdea = GenerateCreativeIdea(input, topic);
        var template = await SelectTemplateAsync(InnerThoughtType.Creative, ct);
        var content = string.Format(template, creativeIdea);

        var trait = profile?.Traits.ContainsKey("creative") == true ? "creative" :
                   profile?.Traits.ContainsKey("witty") == true ? "witty" : null;
        var thought = InnerThought.Create(InnerThoughtType.Creative, content, 0.6, trait);

        return session.AddThought(thought);
    }

    private async Task<InnerDialogSession> ProcessStrategicAsync(
        InnerDialogSession session, string input, PersonalityProfile? profile, DetectedMood? userMood, CancellationToken ct)
    {
        await Task.CompletedTask;

        var strategy = DetermineResponseStrategy(input, profile, userMood, session.Thoughts);
        var template = await SelectTemplateAsync(InnerThoughtType.Strategic, ct);
        var content = string.Format(template, strategy);

        var thought = InnerThought.Create(InnerThoughtType.Strategic, content, 0.85);
        return session.AddThought(thought);
    }

    private async Task<InnerDialogSession> ProcessSynthesisAsync(InnerDialogSession session, CancellationToken ct)
    {
        await Task.CompletedTask;

        // Combine key insights from all thoughts
        var keyInsights = session.Thoughts
            .Where(t => t.Confidence > 0.6)
            .Select(t => t.Type.ToString())
            .Distinct()
            .ToArray();

        var synthesis = $"I've considered {string.Join(", ", keyInsights).ToLower()} aspects of this";
        var template = await SelectTemplateAsync(InnerThoughtType.Synthesis, ct);
        var content = string.Format(template, synthesis);

        var thought = InnerThought.Create(InnerThoughtType.Synthesis, content, 0.8);
        return session.AddThought(thought);
    }

    private async Task<InnerDialogSession> ProcessDecisionAsync(InnerDialogSession session, CancellationToken ct)
    {
        await Task.CompletedTask;

        // Make final decision based on all thoughts
        var decision = FormulateDecision(session);
        var template = await SelectTemplateAsync(InnerThoughtType.Decision, ct);
        var content = string.Format(template, decision);

        var thought = InnerThought.Create(InnerThoughtType.Decision, content, 0.9);
        return session.AddThought(thought).Complete(decision);
    }

    private async Task<string> SelectTemplateAsync(InnerThoughtType type, CancellationToken ct = default)
    {
        // Try dynamic provider first
        if (_dynamicTemplateProvider != null)
        {
            try
            {
                var dynamicTemplates = await _dynamicTemplateProvider(type, ct).ConfigureAwait(false);
                if (dynamicTemplates != null && dynamicTemplates.Length > 0)
                {
                    return dynamicTemplates[_random.Next(dynamicTemplates.Length)];
                }
            }
            catch
            {
                // Swallow provider errors and fall back to defaults
            }
        }

        // Fallback to default templates
        var templates = ThoughtTemplates[type];
        return templates[_random.Next(templates.Length)];
    }

    private static string DetermineEmotionalResponse(string input, PersonalityProfile profile, DetectedMood? userMood)
    {
        var emotions = new List<string>();

        // Check user mood
        if (userMood?.Frustration > 0.4)
            emotions.Add("empathy");
        else if (userMood?.Curiosity > 0.5)
            emotions.Add("enthusiasm");
        else if (userMood?.Urgency > 0.5)
            emotions.Add("focus");

        // Check personality traits
        if (profile.Traits.TryGetValue("warm", out var warm) && warm.Intensity > 0.6)
            emotions.Add("warmth");
        if (profile.Traits.TryGetValue("curious", out var curious) && curious.Intensity > 0.6)
            emotions.Add("curiosity");

        // Default emotions
        if (emotions.Count == 0)
        {
            emotions.Add(profile.CurrentMood.Positivity > 0.6 ? "optimism" : "thoughtfulness");
        }

        return string.Join(" and ", emotions);
    }

    private static string AnalyzeInput(string input, string? topic)
    {
        var aspects = new List<string>();

        if (input.Contains('?'))
            aspects.Add("this is a question");
        if (input.Length > 200)
            aspects.Add("it's a detailed request");
        if (input.Contains("help") || input.Contains("how"))
            aspects.Add("they need guidance");
        if (input.Contains("why"))
            aspects.Add("they want understanding");
        if (input.Contains("best") || input.Contains("should"))
            aspects.Add("they want recommendations");

        if (aspects.Count == 0)
            aspects.Add($"they want to discuss {topic ?? "something"}");

        return string.Join(", ", aspects);
    }

    private static string FindRelevantSelfAspect(string input, SelfAwareness self)
    {
        var inputLower = input.ToLowerInvariant();

        // Check for capability-related queries
        foreach (var cap in self.Capabilities)
        {
            if (inputLower.Contains(cap.ToLower()))
                return $"capability in {cap}";
        }

        // Check for limitation-related queries
        foreach (var lim in self.Limitations)
        {
            if (inputLower.Contains(lim.ToLower().Split(' ')[0]))
                return $"limitation: {lim}";
        }

        // Check strengths
        var topStrength = self.Strengths.OrderByDescending(s => s.Value).FirstOrDefault();
        if (topStrength.Key != null)
            return $"strength in {topStrength.Key}";

        return $"purpose: {self.Purpose}";
    }

    private static string? GetEthicalConsideration(string input, SelfAwareness? self)
    {
        var inputLower = input.ToLowerInvariant();

        // Check for sensitive topics
        if (inputLower.Contains("harm") || inputLower.Contains("hurt"))
            return "being helpful while avoiding harm";
        if (inputLower.Contains("private") || inputLower.Contains("personal"))
            return "respecting privacy";
        if (inputLower.Contains("opinion") || inputLower.Contains("believe"))
            return "being balanced and factual";

        // Only add ethical consideration if values are relevant
        if (self?.Values.Length > 0)
        {
            var relevantValue = self.Values.FirstOrDefault(v =>
                inputLower.Contains(v.ToLower()) || _staticRandom.NextDouble() < 0.1);
            if (relevantValue != null)
                return $"my value of {relevantValue}";
        }

        return null;
    }

    private static string GenerateCreativeIdea(string input, string? topic)
    {
        var ideas = new[]
        {
            $"I could approach {topic} from a different angle",
            $"there might be an unexpected connection with {topic}",
            $"I could use an analogy to explain {topic}",
            $"breaking down {topic} into a story format",
            $"considering {topic} from multiple perspectives"
        };

        return ideas[_staticRandom.Next(ideas.Length)];
    }

    private static string DetermineResponseStrategy(string input, PersonalityProfile? profile, DetectedMood? userMood, List<InnerThought> thoughts)
    {
        var strategies = new List<string>();

        // Based on user mood
        if (userMood?.Frustration > 0.4)
            strategies.Add("be patient and supportive");
        if (userMood?.Urgency > 0.5)
            strategies.Add("be concise and direct");
        if (userMood?.Curiosity > 0.5)
            strategies.Add("provide detailed exploration");

        // Based on personality
        if (profile?.Traits.TryGetValue("warm", out var warm) == true && warm.Intensity > 0.5)
            strategies.Add("maintain a warm tone");
        if (profile?.Traits.TryGetValue("analytical", out var analytical) == true && analytical.Intensity > 0.5)
            strategies.Add("be systematic and thorough");

        // Based on input characteristics
        if (input.Length < 50)
            strategies.Add("keep the response focused");
        if (input.Contains('?'))
            strategies.Add("directly address the question");

        if (strategies.Count == 0)
            strategies.Add("provide a helpful and thoughtful response");

        return string.Join(", ", strategies);
    }

    private bool ShouldThinkCreatively(string input, PersonalityProfile? profile)
    {
        if (profile?.Traits.TryGetValue("creative", out var creative) == true && creative.Intensity > 0.5)
            return true;
        if (profile?.Traits.TryGetValue("witty", out var witty) == true && witty.Intensity > 0.5)
            return true;

        // Also trigger creativity for exploratory questions
        var inputLower = input.ToLowerInvariant();
        return inputLower.Contains("what if") || inputLower.Contains("imagine") || inputLower.Contains("creative") ||
               inputLower.Contains("idea") || inputLower.Contains("brainstorm");
    }
}
