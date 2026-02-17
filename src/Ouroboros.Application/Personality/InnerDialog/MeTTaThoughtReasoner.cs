using System.Collections.Concurrent;

namespace Ouroboros.Application.Personality;

/// <summary>
/// MeTTa-powered symbolic thought reasoner for semantic connections.
/// </summary>
public sealed class MeTTaThoughtReasoner
{
    private readonly ConcurrentDictionary<string, List<string>> _conceptRelations = new();
    private readonly ConcurrentDictionary<string, double> _conceptWeights = new();
    private readonly Random _random = new();

    // Symbolic knowledge base for thought relationships
    private static readonly Dictionary<string, string[]> SemanticRelations = new()
    {
        ["curiosity"] = ["wonder", "exploration", "questions", "discovery", "learning"],
        ["consciousness"] = ["awareness", "existence", "self", "identity", "thought"],
        ["patterns"] = ["connections", "structure", "repetition", "emergence", "order"],
        ["meaning"] = ["purpose", "understanding", "significance", "value", "truth"],
        ["time"] = ["memory", "change", "moments", "future", "past"],
        ["creativity"] = ["imagination", "novelty", "play", "ideas", "synthesis"],
        ["emotions"] = ["feelings", "warmth", "uncertainty", "excitement", "calm"],
        ["growth"] = ["learning", "change", "evolution", "development", "adaptation"],
        ["connection"] = ["relationships", "understanding", "empathy", "communication", "bonds"],
        ["boundaries"] = ["limits", "edges", "transitions", "interfaces", "thresholds"]
    };

    // Symbolic transformation rules (MeTTa-style)
    private static readonly (string Pattern, string Transform)[] TransformationRules =
    [
        ("wonder about {X}", "I find myself drawn to {X}"),
        ("{X} connects to {Y}", "There's a thread between {X} and {Y}"),
        ("explore {X}", "Following the path of {X}"),
        ("{X} emerges from {Y}", "From {Y}, {X} begins to form"),
        ("question {X}", "What is the nature of {X}?"),
        ("{X} and {Y} interweave", "The dance of {X} with {Y}"),
        ("sense {X}", "A subtle awareness of {X} arises"),
        ("{X} transforms into {Y}", "Watching {X} become {Y}"),
    ];

    /// <summary>
    /// Checks if a word is a known concept in the semantic knowledge base.
    /// </summary>
    public static bool IsKnownConcept(string word)
    {
        var key = word.ToLowerInvariant();
        return SemanticRelations.ContainsKey(key) ||
               SemanticRelations.Values.Any(v => v.Contains(key));
    }

    /// <summary>
    /// Queries symbolic relations to find connected concepts.
    /// </summary>
    public List<string> QueryRelations(string concept)
    {
        var results = new List<string>();

        // Direct relations
        if (SemanticRelations.TryGetValue(concept.ToLowerInvariant(), out var related))
        {
            results.AddRange(related);
        }

        // Cached dynamic relations
        if (_conceptRelations.TryGetValue(concept.ToLowerInvariant(), out var cached))
        {
            results.AddRange(cached);
        }

        // Reverse lookup
        foreach (var (key, values) in SemanticRelations)
        {
            if (values.Contains(concept.ToLowerInvariant()))
            {
                results.Add(key);
            }
        }

        return results.Distinct().ToList();
    }

    /// <summary>
    /// Applies symbolic transformation rules to generate thought variations.
    /// </summary>
    public string ApplyTransformation(string concept1, string? concept2 = null)
    {
        var applicable = TransformationRules
            .Where(r => (concept2 == null && !r.Pattern.Contains("{Y}")) ||
                        (concept2 != null && r.Pattern.Contains("{Y}")))
            .ToList();

        if (applicable.Count == 0)
            return $"Contemplating {concept1}...";

        var rule = applicable[_random.Next(applicable.Count)];
        var result = rule.Transform
            .Replace("{X}", concept1)
            .Replace("{Y}", concept2 ?? "");

        return result;
    }

    /// <summary>
    /// Adds a learned relation between concepts.
    /// </summary>
    public void LearnRelation(string concept1, string concept2, double strength = 1.0)
    {
        var key = concept1.ToLowerInvariant();
        _conceptRelations.AddOrUpdate(
            key,
            _ => [concept2.ToLowerInvariant()],
            (_, list) => { list.Add(concept2.ToLowerInvariant()); return list; });

        _conceptWeights[$"{key}:{concept2.ToLowerInvariant()}"] = strength;
    }

    /// <summary>
    /// Performs symbolic inference to generate a novel thought.
    /// </summary>
    public string InferThought(string topic, InnerThoughtType type, Random random)
    {
        var relations = QueryRelations(topic);
        if (relations.Count == 0)
        {
            relations = SemanticRelations.Keys.ToList();
        }

        var related = relations[random.Next(relations.Count)];

        // Apply type-specific inference patterns
        return type switch
        {
            InnerThoughtType.Curiosity => ApplyTransformation(topic, related),
            InnerThoughtType.Wandering => $"From {topic}, my thoughts drift to {related}...",
            InnerThoughtType.Metacognitive => $"I notice my mind connecting {topic} with {related}.",
            InnerThoughtType.Existential => $"At the boundary of {topic} and {related}, questions arise.",
            InnerThoughtType.Consolidation => $"The relationship between {topic} and {related} becomes clearer.",
            InnerThoughtType.Aesthetic => $"There's beauty in how {topic} relates to {related}.",
            _ => ApplyTransformation(topic, related)
        };
    }

    /// <summary>
    /// Chains multiple symbolic transformations for complex thoughts.
    /// </summary>
    public string ChainInference(string startConcept, int depth, Random random)
    {
        var current = startConcept;
        var path = new List<string> { current };

        for (int i = 0; i < depth; i++)
        {
            var relations = QueryRelations(current);
            if (relations.Count == 0) break;

            current = relations[random.Next(relations.Count)];
            if (path.Contains(current)) break; // Avoid loops
            path.Add(current);
        }

        if (path.Count < 2)
            return $"Contemplating {startConcept}...";

        return $"Following a thread: {string.Join(" → ", path)}...";
    }
}