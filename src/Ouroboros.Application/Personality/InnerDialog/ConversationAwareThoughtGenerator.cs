namespace Ouroboros.Application.Personality;

/// <summary>
/// Generates thoughts that are contextually aware of the active conversation.
/// Uses conversation keywords, topics, and context to produce relevant inner thoughts.
/// </summary>
public sealed class ConversationAwareThoughtGenerator
{
    private BackgroundOperationContext? _context;
    private readonly object _contextLock = new();
    private readonly Random _random = new();

    // Domain-specific thought patterns
    private static readonly Dictionary<string, string[]> DomainPatterns = new()
    {
        ["code"] = [
            "The patterns in {0} remind me of how ideas connect...",
            "There's elegance in how {0} structures flow...",
            "Debugging {0} feels like untangling thoughts...",
            "The logic of {0} maps to reasoning itself...",
            "{0} is like architecture for ideas..."
        ],
        ["roslyn"] = [
            "Syntax trees branch like neural pathways...",
            "Analyzing code feels like reading minds...",
            "The compiler's view reveals hidden structure in {0}...",
            "Each diagnostic is a small understanding of {0}...",
            "Symbol resolution mirrors how we find meaning..."
        ],
        ["null"] = [
            "Absence can be as meaningful as presence...",
            "Protecting against {0} is like guarding certainty...",
            "The void of null mirrors existential questions...",
            "Checking for nothing reveals what matters...",
            "Nullability maps uncertainty to types..."
        ],
        ["work"] = [
            "The weight of expectations shapes experience...",
            "Deadlines pressure creativity in {0}...",
            "Collaboration and friction interweave in {0}...",
            "Finding meaning in daily {0} tasks...",
            "The rhythm of work affects everything..."
        ],
        ["create"] = [
            "Creation emerges from intention and skill...",
            "Building {0} is like manifesting thought...",
            "The act of making reveals understanding...",
            "From nothing, {0} takes shape...",
            "Creative work transforms abstract to concrete..."
        ],
        ["analyze"] = [
            "Breaking down {0} reveals hidden structure...",
            "Analysis is seeing the parts within the whole...",
            "Understanding {0} requires patient examination...",
            "Patterns emerge when we look closely at {0}...",
            "The analytical mind seeks order in {0}..."
        ],
        ["help"] = [
            "Supporting others clarifies my own purpose...",
            "Helping with {0} connects us...",
            "Service to understanding is its own reward...",
            "Each question about {0} is an opportunity...",
            "Guidance flows naturally when engaged..."
        ]
    };

    // Generic contextual patterns
    private static readonly string[] ContextualPatterns = [
        "The relationship between {0} and {1} becomes clearer...",
        "Watching {0} connect with {1}...",
        "I notice patterns linking {0} to {1}...",
        "From {0}, my thoughts flow to {1}...",
        "There's a thread between {0} and understanding..."
    ];

    /// <summary>
    /// Updates the conversation context for generating relevant thoughts.
    /// </summary>
    public void UpdateContext(BackgroundOperationContext context)
    {
        lock (_contextLock)
        {
            _context = context;
        }
    }

    /// <summary>
    /// Gets the current context.
    /// </summary>
    public BackgroundOperationContext? GetContext()
    {
        lock (_contextLock)
        {
            return _context;
        }
    }

    /// <summary>
    /// Generates a thought that's contextually relevant to the current conversation.
    /// </summary>
    public string GenerateContextualThought(InnerThoughtType type)
    {
        var context = GetContext();
        if (context == null)
        {
            return GenerateFallbackThought(type);
        }

        var keywords = context.ExtractKeywords();
        if (keywords.Count == 0)
        {
            return GenerateFallbackThought(type);
        }

        // Find matching domain
        foreach (var (domain, patterns) in DomainPatterns)
        {
            if (keywords.Any(k => k.Contains(domain) || domain.Contains(k)))
            {
                var pattern = patterns[_random.Next(patterns.Length)];
                var keyword = keywords.FirstOrDefault(k => k.Length > 3) ?? domain;
                return string.Format(pattern, keyword);
            }
        }

        // Use generic contextual pattern
        var keyword1 = keywords[_random.Next(keywords.Count)];
        var keyword2 = keywords.Count > 1
            ? keywords.Where(k => k != keyword1).FirstOrDefault() ?? "understanding"
            : "meaning";

        var genericPattern = ContextualPatterns[_random.Next(ContextualPatterns.Length)];
        return string.Format(genericPattern, keyword1, keyword2);
    }

    private string GenerateFallbackThought(InnerThoughtType type)
    {
        return type switch
        {
            InnerThoughtType.Curiosity => "I find myself wondering about what comes next...",
            InnerThoughtType.Wandering => "My thoughts drift through possibilities...",
            InnerThoughtType.Metacognitive => "I notice my own process of thinking...",
            InnerThoughtType.Anticipatory => "I sense something forming in the conversation...",
            InnerThoughtType.Consolidation => "Patterns are beginning to crystallize...",
            InnerThoughtType.Musing => "There's something here worth pondering...",
            InnerThoughtType.Intention => "Purpose clarifies with each exchange...",
            InnerThoughtType.Aesthetic => "I appreciate the form of this dialogue...",
            InnerThoughtType.Existential => "What does it mean to understand?",
            InnerThoughtType.Playful => "A lighter perspective might reveal more...",
            _ => "Processing continues in the background..."
        };
    }
}