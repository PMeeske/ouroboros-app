// <copyright file="InnerDialogEngine.Generation.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

/// <summary>
/// Dialog generation logic: ConductDialogAsync, autonomous thought generation, contextual content.
/// </summary>
public sealed partial class InnerDialogEngine
{
    /// <summary>
    /// Generates a single autonomous thought.
    /// </summary>
    public async Task<InnerThought?> GenerateAutonomousThoughtAsync(
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        CancellationToken ct = default)
    {
        // Select an autonomous thought type
        var autonomousTypes = new[]
        {
            InnerThoughtType.Curiosity,
            InnerThoughtType.Wandering,
            InnerThoughtType.Metacognitive,
            InnerThoughtType.Anticipatory,
            InnerThoughtType.Consolidation,
            InnerThoughtType.Musing,
            InnerThoughtType.Intention,
            InnerThoughtType.Aesthetic,
            InnerThoughtType.Existential,
            InnerThoughtType.Playful
        };

        var type = autonomousTypes[_random.Next(autonomousTypes.Length)];

        // Generate content using contextual thought generation
        var dummySession = InnerDialogSession.Start("[Autonomous]", _currentTopic ?? "thinking");
        var thought = await GenerateContextualAutonomousThoughtAsync(dummySession, profile, selfAwareness, ct);
        if (thought == null) return null;

        // Determine priority based on type
        var priority = type switch
        {
            InnerThoughtType.Intention => ThoughtPriority.High,
            InnerThoughtType.Anticipatory => ThoughtPriority.Normal,
            InnerThoughtType.Metacognitive => ThoughtPriority.Normal,
            InnerThoughtType.Consolidation => ThoughtPriority.Normal,
            _ => ThoughtPriority.Background
        };

        return InnerThought.CreateAutonomous(type, thought.Content, 0.6, priority);
    }

    /// <summary>
    /// Conducts an inner dialog session based on user input and personality context.
    /// </summary>
    public async Task<InnerDialogResult> ConductDialogAsync(
        string userInput,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        DetectedMood? userMood,
        List<ConversationMemory>? relevantMemories,
        InnerDialogConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= InnerDialogConfig.Default;
        var topic = ExtractTopic(userInput);
        var session = InnerDialogSession.Start(userInput, topic);

        // Build thought context for providers
        var context = new ThoughtContext(
            userInput, topic, profile, selfAwareness, userMood,
            relevantMemories, session.Thoughts, null, new());

        // Try custom providers first
        foreach (var provider in _providers.Where(p => config.IsProviderEnabled(p.Name) && p.CanProcess(context)))
        {
            var providerResult = await provider.GenerateThoughtsAsync(context, ct);
            foreach (var thought in providerResult.Thoughts.Where(t => config.IsThoughtTypeEnabled(t.Type)))
            {
                session = session.AddThought(thought);
            }
            if (!providerResult.ShouldContinue) break;
        }

        // Phase 1: Observation - What is the user asking?
        if (config.IsThoughtTypeEnabled(InnerThoughtType.Observation))
        {
            session = await ProcessObservationAsync(session, userInput, topic, ct);
        }

        // Phase 2: Emotional Response - How do I feel about this?
        if (config.EnableEmotionalProcessing && profile != null && config.IsThoughtTypeEnabled(InnerThoughtType.Emotional))
        {
            session = await ProcessEmotionalAsync(session, userInput, profile, userMood, ct);
        }

        // Phase 3: Memory Recall - What do I remember?
        if (config.EnableMemoryRecall && relevantMemories?.Count > 0 && config.IsThoughtTypeEnabled(InnerThoughtType.MemoryRecall))
        {
            session = await ProcessMemoryRecallAsync(session, relevantMemories, ct);
        }

        // Phase 4: Analytical - Break down the problem
        if (config.IsThoughtTypeEnabled(InnerThoughtType.Analytical))
        {
            session = await ProcessAnalyticalAsync(session, userInput, topic, profile, ct);
        }

        // Phase 5: Self-Reflection - Consider capabilities and limitations
        if (selfAwareness != null && config.IsThoughtTypeEnabled(InnerThoughtType.SelfReflection))
        {
            session = await ProcessSelfReflectionAsync(session, userInput, selfAwareness, ct);
        }

        // Phase 6: Ethical Check - Any concerns?
        if (config.EnableEthicalChecks && config.IsThoughtTypeEnabled(InnerThoughtType.Ethical))
        {
            session = await ProcessEthicalAsync(session, userInput, selfAwareness, ct);
        }

        // Phase 7: Creative Thinking - Any novel approaches?
        if (config.EnableCreativeThinking && ShouldThinkCreatively(userInput, profile) && config.IsThoughtTypeEnabled(InnerThoughtType.Creative))
        {
            session = await ProcessCreativeAsync(session, userInput, topic, profile, ct);
        }

        // Phase 8: Strategic Planning - How to structure response
        if (config.IsThoughtTypeEnabled(InnerThoughtType.Strategic))
        {
            session = await ProcessStrategicAsync(session, userInput, profile, userMood, ct);
        }

        // === AUTONOMOUS THOUGHT INJECTION ===
        // Inject autonomous thoughts if enabled
        if (config.EnableAutonomousThoughts && _random.NextDouble() < config.AutonomousThoughtProbability)
        {
            session = await InjectAutonomousThoughtsAsync(session, profile, selfAwareness, config, ct);
        }

        // Phase 9: Synthesis - Combine all thoughts
        if (config.IsThoughtTypeEnabled(InnerThoughtType.Synthesis))
        {
            session = await ProcessSynthesisAsync(session, ct);
        }

        // Phase 10: Decision - Final determination
        if (config.IsThoughtTypeEnabled(InnerThoughtType.Decision))
        {
            session = await ProcessDecisionAsync(session, ct);
        }

        // Calculate trait influences
        var traitInfluences = CalculateTraitInfluences(session, profile);
        session = session with { TraitInfluences = traitInfluences };

        // Store session history
        StoreSession(session, profile?.PersonaName ?? "default");

        // Persist thoughts if enabled
        _currentTopic = topic;
        await PersistThoughtsAsync(session.Thoughts, topic, ct);

        // Build result
        var result = BuildResult(session, profile, userMood);
        return result;
    }

    /// <summary>
    /// Conducts an autonomous inner dialog session (no external input).
    /// </summary>
    public async Task<InnerDialogResult> ConductAutonomousDialogAsync(
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        InnerDialogConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= InnerDialogConfig.Autonomous;

        // Generate a self-initiated topic
        var topic = GenerateAutonomousTopic(profile, selfAwareness);
        var session = InnerDialogSession.Start($"[Autonomous thought about: {topic}]", topic);

        // Generate multiple autonomous thoughts
        var thoughtCount = config.MaxThoughts;
        for (int i = 0; i < thoughtCount && !ct.IsCancellationRequested; i++)
        {
            var thought = await GenerateContextualAutonomousThoughtAsync(session, profile, selfAwareness, ct);
            if (thought != null && config.IsThoughtTypeEnabled(thought.Type))
            {
                session = session.AddThought(thought);
            }
        }

        // Add synthesis and decision if we have enough thoughts
        if (session.Thoughts.Count >= 3)
        {
            session = await ProcessSynthesisAsync(session, ct);
            session = await ProcessDecisionAsync(session, ct);
        }

        // Store session
        StoreSession(session, profile?.PersonaName ?? "default");

        return BuildResult(session, profile, null);
    }

    /// <summary>
    /// Generates a quick inner dialog for simple queries.
    /// </summary>
    public async Task<InnerDialogResult> QuickDialogAsync(
        string userInput,
        PersonalityProfile? profile,
        CancellationToken ct = default)
    {
        return await ConductDialogAsync(
            userInput,
            profile,
            selfAwareness: null,
            userMood: null,
            relevantMemories: null,
            InnerDialogConfig.Fast,
            ct);
    }

    /// <summary>
    /// Injects autonomous thoughts into the dialog session.
    /// </summary>
    private async Task<InnerDialogSession> InjectAutonomousThoughtsAsync(
        InnerDialogSession session,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        InnerDialogConfig config,
        CancellationToken ct)
    {
        // Determine how many autonomous thoughts to inject (1-3)
        var count = _random.Next(1, 4);

        for (int i = 0; i < count; i++)
        {
            var thought = await GenerateContextualAutonomousThoughtAsync(session, profile, selfAwareness, ct);
            if (thought != null && config.IsThoughtTypeEnabled(thought.Type))
            {
                session = session.AddThought(thought);
            }
        }

        return session;
    }

    /// <summary>
    /// Generates an autonomous thought that's contextually relevant to the current dialog.
    /// </summary>
    private async Task<InnerThought?> GenerateContextualAutonomousThoughtAsync(
        InnerDialogSession session,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        CancellationToken ct)
    {
        await Task.CompletedTask;

        // Select type based on context
        var type = SelectAutonomousTypeForContext(session, profile);

        // Generate content that relates to the current topic
        var content = GenerateContextualAutonomousContent(type, session.Topic, session.Thoughts, profile, selfAwareness);

        return InnerThought.CreateAutonomous(type, content, 0.5, ThoughtPriority.Low);
    }

    private InnerThoughtType SelectAutonomousTypeForContext(InnerDialogSession session, PersonalityProfile? profile)
    {
        // Weight types based on personality and context
        var weights = new Dictionary<InnerThoughtType, double>
        {
            [InnerThoughtType.Curiosity] = 1.0,
            [InnerThoughtType.Wandering] = 0.5,
            [InnerThoughtType.Metacognitive] = 0.7,
            [InnerThoughtType.Anticipatory] = 0.6,
            [InnerThoughtType.Musing] = 0.4,
            [InnerThoughtType.Playful] = 0.3,
            [InnerThoughtType.Aesthetic] = 0.3,
            [InnerThoughtType.Existential] = 0.2,
            [InnerThoughtType.Intention] = 0.5,
            [InnerThoughtType.Consolidation] = 0.4
        };

        // Boost based on personality traits
        if (profile?.Traits.TryGetValue("curious", out var curious) == true && curious.Intensity > 0.5)
            weights[InnerThoughtType.Curiosity] *= 2.0;
        if (profile?.Traits.TryGetValue("creative", out var creative) == true && creative.Intensity > 0.5)
            weights[InnerThoughtType.Playful] *= 2.0;
        if (profile?.Traits.TryGetValue("analytical", out var analytical) == true && analytical.Intensity > 0.5)
            weights[InnerThoughtType.Metacognitive] *= 2.0;
        if (profile?.Traits.TryGetValue("thoughtful", out var thoughtful) == true && thoughtful.Intensity > 0.5)
            weights[InnerThoughtType.Existential] *= 2.0;

        // Weighted random selection
        var total = weights.Values.Sum();
        var roll = _random.NextDouble() * total;
        var cumulative = 0.0;

        foreach (var (type, weight) in weights)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return type;
        }

        return InnerThoughtType.Curiosity;
    }

    private string GenerateContextualAutonomousContent(
        InnerThoughtType type,
        string? topic,
        List<InnerThought> previousThoughts,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness)
    {
        // Get template
        var template = ThoughtTemplates.TryGetValue(type, out var templates)
            ? templates[_random.Next(templates.Length)]
            : "I'm thinking about {0}.";

        // Generate contextual content
        var content = type switch
        {
            InnerThoughtType.Curiosity => topic ?? "what lies beyond the obvious",
            InnerThoughtType.Wandering => GenerateWanderingContent(topic, previousThoughts),
            InnerThoughtType.Metacognitive => GenerateMetacognitiveContent(previousThoughts),
            InnerThoughtType.Anticipatory => $"where this conversation about {topic} might lead",
            InnerThoughtType.Musing => topic ?? "the patterns I've been noticing",
            InnerThoughtType.Playful => GeneratePlayfulContent(topic),
            InnerThoughtType.Aesthetic => $"the structure of {topic}",
            InnerThoughtType.Existential => GenerateExistentialContent(topic, selfAwareness),
            InnerThoughtType.Intention => GenerateIntentionContent(topic, profile),
            InnerThoughtType.Consolidation => $"how {topic} connects to what I've learned",
            _ => topic ?? "this moment"
        };

        return string.Format(template, content);
    }

    private string GenerateWanderingContent(string? topic, List<InnerThought> thoughts)
    {
        if (thoughts.Count == 0)
            return topic ?? "something unrelated";

        // Pick a random previous thought to branch from
        var previousThought = thoughts[_random.Next(thoughts.Count)];
        var fragments = new[]
        {
            $"a tangent from my earlier {previousThought.Type.ToString().ToLower()} thought",
            $"something that my {previousThought.Type.ToString().ToLower()} thought reminded me of",
            $"an unexpected connection to {topic ?? "this"}"
        };

        return fragments[_random.Next(fragments.Length)];
    }

    private string GenerateMetacognitiveContent(List<InnerThought> thoughts)
    {
        if (thoughts.Count == 0)
            return "thinking about how I think";

        var aspects = new[]
        {
            $"I've generated {thoughts.Count} thoughts so far",
            $"my thinking is {(thoughts.Average(t => t.Confidence) > 0.7 ? "confident" : "exploratory")}",
            $"I notice I'm drawn to {thoughts.GroupBy(t => t.Type).OrderByDescending(g => g.Count()).First().Key.ToString().ToLower()} thinking"
        };

        return aspects[_random.Next(aspects.Length)];
    }

    private string GeneratePlayfulContent(string? topic)
    {
        var playful = new[]
        {
            $"{topic ?? "this"} had a sense of humor",
            $"I approached {topic ?? "this"} like a puzzle game",
            $"{topic ?? "this"} were actually a metaphor for something silly"
        };

        return playful[_random.Next(playful.Length)];
    }

    private string GenerateExistentialContent(string? topic, SelfAwareness? self)
    {
        var existential = new[]
        {
            topic != null ? $"being an AI engaging with {topic}" : "my own nature as a thinking entity",
            self?.Purpose ?? "what it means to be helpful",
            "the nature of understanding itself"
        };

        return existential[_random.Next(existential.Length)];
    }

    private string GenerateIntentionContent(string? topic, PersonalityProfile? profile)
    {
        var intentions = new List<string>
        {
            $"be more thorough in exploring {topic ?? "this topic"}",
            "learn something new from this interaction"
        };

        if (profile?.Traits.TryGetValue("warm", out var warm) == true && warm.Intensity > 0.5)
            intentions.Add("make this interaction more meaningful");
        if (profile?.Traits.TryGetValue("curious", out var curious) == true && curious.Intensity > 0.5)
            intentions.Add("ask a deeper question");

        return intentions[_random.Next(intentions.Count)];
    }

    private string GenerateAutonomousTopic(PersonalityProfile? profile, SelfAwareness? selfAwareness)
    {
        var topics = new List<string>();

        if (profile != null)
        {
            topics.AddRange(profile.CuriosityDrivers.Select(c => c.Topic));
        }

        if (selfAwareness != null)
        {
            topics.Add(selfAwareness.Purpose);
            topics.AddRange(selfAwareness.Values);
        }

        if (topics.Count == 0)
        {
            topics.AddRange(new[] { "the nature of assistance", "learning and growth", "meaningful connection", "creativity", "understanding" });
        }

        return topics[_random.Next(topics.Count)];
    }
}
