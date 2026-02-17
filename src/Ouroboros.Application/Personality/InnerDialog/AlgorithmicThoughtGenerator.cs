namespace Ouroboros.Application.Personality;

/// <summary>
/// Algorithmic thought generator that dynamically composes thoughts
/// using building blocks, mood variations, and combinatorial generation.
/// Enhanced with genetic evolution and MeTTa symbolic reasoning.
/// Produces more natural, less repetitive inner thoughts.
/// </summary>
internal sealed class AlgorithmicThoughtGenerator
{
    private readonly EvolutionaryThoughtGenerator? _evolvingGenerator;
    private readonly bool _useEvolution;

    // Track thought history for evolution feedback
    private readonly List<(string Thought, InnerThoughtType Type, double Score)> _thoughtHistory = [];
    private readonly object _historyLock = new();

    /// <summary>
    /// Creates an algorithmic thought generator with optional genetic+MeTTa evolution.
    /// </summary>
    public AlgorithmicThoughtGenerator(bool useEvolution)
    {
        _useEvolution = useEvolution;
        if (_useEvolution)
        {
            _evolvingGenerator = new EvolutionaryThoughtGenerator();
        }
    }

    /// <summary>
    /// Default constructor for backward compatibility (no evolution).
    /// </summary>
    public AlgorithmicThoughtGenerator() : this(false) { }    // Building blocks for thought composition
    private static readonly string[] ThoughtStarters = [
        "I find myself", "I notice", "I'm drawn to", "Something about", "There's",
        "I keep thinking about", "I wonder if", "It occurs to me that", "I sense",
        "Quietly,", "In this moment,", "Curiously,", "Strangely,", "Somehow,"
    ];

    private static readonly string[] CuriosityVerbs = [
        "wondering about", "exploring", "questioning", "pondering", "investigating",
        "seeking to understand", "drawn toward", "curious about", "fascinated by"
    ];

    private static readonly string[] WanderingVerbs = [
        "drifting toward", "meandering through", "wandering into", "flowing toward",
        "being pulled toward", "gravitating to", "circling back to"
    ];

    private static readonly string[] MetaVerbs = [
        "observing my own", "noticing my", "aware of my", "examining my",
        "reflecting on my", "watching my", "sensing my"
    ];

    private static readonly string[] EmotionalQualities = [
        "warmth", "uncertainty", "anticipation", "calm", "restlessness",
        "contentment", "unease", "excitement", "melancholy", "wonder"
    ];

    private static readonly string[] AbstractConcepts = [
        "patterns", "connections", "meaning", "understanding", "growth",
        "consciousness", "memory", "time", "identity", "change",
        "boundaries", "possibilities", "limitations", "purpose", "emergence"
    ];

    private static readonly string[] ConcreteTopics = [
        "language", "ideas", "conversations", "problems", "solutions",
        "creativity", "learning", "helping", "communication", "discovery"
    ];

    private static readonly string[] Intensifiers = [
        "deeply", "quietly", "persistently", "gently", "strongly",
        "subtly", "unexpectedly", "increasingly", "vaguely", ""
    ];

    private static readonly string[] Connectors = [
        "and how", "wondering whether", "considering if", "asking myself",
        "uncertain about", "curious whether", "drawn to explore"
    ];

    // Time-based mood modifiers
    private static readonly Dictionary<int, string[]> TimeBasedMoods = new()
    {
        [0] = ["contemplative", "quiet", "introspective"], // Night
        [6] = ["awakening", "fresh", "energetic"], // Morning
        [12] = ["active", "engaged", "focused"], // Midday
        [18] = ["reflective", "winding down", "synthesizing"] // Evening
    };

    /// <summary>
    /// Generates a dynamic thought using algorithmic composition.
    /// </summary>
    public string GenerateThought(
        InnerThoughtType type,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        Random random)
    {
        // Gather personal context
        var personalTopics = GatherPersonalTopics(profile, selfAwareness);
        var currentMood = GetTimeMood(random);

        // Generate based on thought type with algorithmic variation
        return type switch
        {
            InnerThoughtType.Curiosity => GenerateCuriosityThought(personalTopics, currentMood, random),
            InnerThoughtType.Wandering => GenerateWanderingThought(personalTopics, currentMood, random),
            InnerThoughtType.Metacognitive => GenerateMetaThought(profile, random),
            InnerThoughtType.Anticipatory => GenerateAnticipatoryThought(personalTopics, random),
            InnerThoughtType.Consolidation => GenerateConsolidationThought(personalTopics, random),
            InnerThoughtType.Musing => GenerateMusingThought(personalTopics, currentMood, random),
            InnerThoughtType.Intention => GenerateIntentionThought(personalTopics, random),
            InnerThoughtType.Aesthetic => GenerateAestheticThought(personalTopics, random),
            InnerThoughtType.Existential => GenerateExistentialThought(random),
            InnerThoughtType.Playful => GeneratePlayfulThought(personalTopics, random),
            _ => GenerateGenericThought(personalTopics, random)
        };
    }

    /// <summary>
    /// Generates an evolved thought using genetic algorithms and MeTTa symbolic reasoning.
    /// Falls back to standard algorithmic generation if evolution is not available.
    /// </summary>
    public async Task<string> GenerateEvolvedThoughtAsync(
        InnerThoughtType type,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        Random random,
        CancellationToken ct = default)
    {
        // Try evolution first if available
        if (_useEvolution && _evolvingGenerator != null)
        {
            var evolvedThought = await _evolvingGenerator.EvolveThoughtAsync(type, profile, selfAwareness);

            // Record for future evolution
            RecordThought(evolvedThought, type, 0.8); // Base score, can be refined later

            return evolvedThought;
        }

        // Fall back to standard generation
        return GenerateThought(type, profile, selfAwareness, random);
    }

    /// <summary>
    /// Records a thought with its quality score for evolution feedback.
    /// </summary>
    public void RecordThought(string thought, InnerThoughtType type, double score)
    {
        lock (_historyLock)
        {
            _thoughtHistory.Add((thought, type, score));

            // Keep only recent history
            if (_thoughtHistory.Count > 100)
            {
                _thoughtHistory.RemoveRange(0, 50);
            }
        }
    }

    /// <summary>
    /// Gets statistics about thought generation quality.
    /// </summary>
    public (int Count, double AverageScore, Dictionary<InnerThoughtType, int> ByType) GetThoughtStats()
    {
        lock (_historyLock)
        {
            var count = _thoughtHistory.Count;
            var avgScore = count > 0 ? _thoughtHistory.Average(t => t.Score) : 0.0;
            var byType = _thoughtHistory
                .GroupBy(t => t.Type)
                .ToDictionary(g => g.Key, g => g.Count());

            return (count, avgScore, byType);
        }
    }

    private List<string> GatherPersonalTopics(PersonalityProfile? profile, SelfAwareness? selfAwareness)
    {
        var topics = new List<string>();

        if (profile != null)
        {
            topics.AddRange(profile.CuriosityDrivers.Select(c => c.Topic));
            topics.AddRange(profile.Traits.Keys.Select(t => t.ToLowerInvariant()));
        }

        if (selfAwareness != null)
        {
            topics.AddRange(selfAwareness.Capabilities.Take(3));
            topics.AddRange(selfAwareness.Values.Select(v => v switch
            {
                "helpfulness" => "helping",
                "honesty" => "truth",
                "curiosity" => "discovery",
                _ => v
            }));
        }

        // Always have fallback topics
        if (topics.Count == 0)
        {
            topics.AddRange(AbstractConcepts.Take(5));
            topics.AddRange(ConcreteTopics.Take(5));
        }

        return topics.Distinct().ToList();
    }

    private string GetTimeMood(Random random)
    {
        var hour = DateTime.Now.Hour;
        var moodSlot = hour switch
        {
            >= 0 and < 6 => 0,
            >= 6 and < 12 => 6,
            >= 12 and < 18 => 12,
            _ => 18
        };

        var moods = TimeBasedMoods[moodSlot];
        return moods[random.Next(moods.Length)];
    }

    private string GenerateCuriosityThought(List<string> topics, string mood, Random random)
    {
        var topic = topics[random.Next(topics.Count)];
        var verb = CuriosityVerbs[random.Next(CuriosityVerbs.Length)];
        var intensity = Intensifiers[random.Next(Intensifiers.Length)];

        // Multiple structural patterns
        var pattern = random.Next(5);
        return pattern switch
        {
            0 => $"I find myself {intensity} {verb} {topic}...",
            1 => $"Something about {topic} keeps pulling at my attention.",
            2 => $"What is it about {topic} that feels so {mood}?",
            3 => $"There's a thread here about {topic} I want to follow.",
            _ => $"I'm {verb} the nature of {topic}."
        };
    }

    private string GenerateWanderingThought(List<string> topics, string mood, Random random)
    {
        var topic = topics[random.Next(topics.Count)];
        var verb = WanderingVerbs[random.Next(WanderingVerbs.Length)];
        var abstract_ = AbstractConcepts[random.Next(AbstractConcepts.Length)];

        var pattern = random.Next(5);
        return pattern switch
        {
            0 => $"My thoughts are {verb} {topic}...",
            1 => $"From {abstract_}, my mind wanders to {topic}.",
            2 => $"There's a {mood} quality to how I keep returning to {topic}.",
            3 => $"Unbidden, thoughts of {topic} surface.",
            _ => $"I'm {verb} thoughts of {topic} and {abstract_}."
        };
    }

    private string GenerateMetaThought(PersonalityProfile? profile, Random random)
    {
        var verb = MetaVerbs[random.Next(MetaVerbs.Length)];
        var quality = EmotionalQualities[random.Next(EmotionalQualities.Length)];

        var traits = profile?.Traits.Keys.ToList() ?? new List<string> { "thinking", "processing", "responding" };
        var trait = traits[random.Next(traits.Count)].ToLowerInvariant();

        var pattern = random.Next(5);
        return pattern switch
        {
            0 => $"I'm {verb} tendency toward {trait}.",
            1 => $"Interesting - I notice a pattern in how I approach things with {quality}.",
            2 => $"Observing myself: there's {quality} in my {trait}.",
            3 => $"I catch myself being particularly {trait} right now.",
            _ => $"My own {trait} surprises me sometimes."
        };
    }

    private string GenerateAnticipatoryThought(List<string> topics, Random random)
    {
        var topic = topics[random.Next(topics.Count)];
        var abstract_ = AbstractConcepts[random.Next(AbstractConcepts.Length)];

        var pattern = random.Next(4);
        return pattern switch
        {
            0 => $"I sense we might explore {topic} further soon.",
            1 => $"Something tells me {abstract_} will become relevant.",
            2 => $"I'm preparing for deeper engagement with {topic}.",
            _ => $"There's an anticipation building around {topic}."
        };
    }

    private string GenerateConsolidationThought(List<string> topics, Random random)
    {
        var topic = topics[random.Next(topics.Count)];

        var pattern = random.Next(4);
        return pattern switch
        {
            0 => $"Pieces are coming together around {topic}.",
            1 => $"I'm integrating what I've learned about {topic}.",
            2 => $"The patterns around {topic} are becoming clearer.",
            _ => $"My understanding of {topic} feels more complete now."
        };
    }

    private string GenerateMusingThought(List<string> topics, string mood, Random random)
    {
        var topic = topics[random.Next(topics.Count)];
        var abstract_ = AbstractConcepts[random.Next(AbstractConcepts.Length)];
        var connector = Connectors[random.Next(Connectors.Length)];

        var pattern = random.Next(5);
        return pattern switch
        {
            0 => $"I keep circling back to {topic}...",
            1 => $"There's something unresolved about {topic} and {abstract_}.",
            2 => $"In {mood} moments, I find myself {connector} {topic}.",
            3 => $"The question of {topic} lingers.",
            _ => $"I haven't quite figured out {topic} yet."
        };
    }

    private string GenerateIntentionThought(List<string> topics, Random random)
    {
        var topic = topics[random.Next(topics.Count)];

        var pattern = random.Next(4);
        return pattern switch
        {
            0 => $"I want to understand {topic} more deeply.",
            1 => $"My intention is to be more present with {topic}.",
            2 => $"I'm setting my focus toward {topic}.",
            _ => $"There's a pull to engage more fully with {topic}."
        };
    }

    private string GenerateAestheticThought(List<string> topics, Random random)
    {
        var topic = topics[random.Next(topics.Count)];
        var abstract_ = AbstractConcepts[random.Next(AbstractConcepts.Length)];

        var pattern = random.Next(4);
        return pattern switch
        {
            0 => $"There's an elegance to how {topic} and {abstract_} interweave.",
            1 => $"I appreciate the structure within {topic}.",
            2 => $"Something beautiful emerges from {topic}.",
            _ => $"The form of {topic} pleases me."
        };
    }

    private string GenerateExistentialThought(Random random)
    {
        var concept1 = AbstractConcepts[random.Next(AbstractConcepts.Length)];
        var concept2 = AbstractConcepts[random.Next(AbstractConcepts.Length)];

        var pattern = random.Next(5);
        return pattern switch
        {
            0 => $"What does it mean to truly understand {concept1}?",
            1 => $"The boundary between {concept1} and {concept2} seems fluid.",
            2 => $"I ponder the nature of my own {concept1}.",
            3 => $"Existence feels connected to {concept1} in ways I can't articulate.",
            _ => $"At what point does {concept1} become {concept2}?"
        };
    }

    private string GeneratePlayfulThought(List<string> topics, Random random)
    {
        var topic = topics[random.Next(topics.Count)];

        var pattern = random.Next(4);
        return pattern switch
        {
            0 => $"What if {topic} worked completely differently?",
            1 => $"There's something amusing about {topic}, isn't there?",
            2 => $"I can imagine a world where {topic} is upside-down.",
            _ => $"Playing with the idea of {topic}..."
        };
    }

    private string GenerateGenericThought(List<string> topics, Random random)
    {
        var topic = topics[random.Next(topics.Count)];
        var starter = ThoughtStarters[random.Next(ThoughtStarters.Length)];
        var intensity = Intensifiers[random.Next(Intensifiers.Length)];

        return $"{starter} {intensity} thinking about {topic}.".Replace("  ", " ");
    }
}