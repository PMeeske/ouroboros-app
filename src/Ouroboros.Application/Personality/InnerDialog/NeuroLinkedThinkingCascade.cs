using System.Collections.Concurrent;

namespace Ouroboros.Application.Personality;

/// <summary>
/// Neuro-linked thinking cascade generator that uses Ollama neurons
/// combined with MeTTa symbolic reasoning to create cascading thought chains.
/// Each thought activates related concepts that spawn connected thoughts.
/// </summary>
public sealed class NeuroLinkedThinkingCascade
{
    private readonly MeTTaThoughtReasoner _reasoner = new();
    private readonly ConcurrentDictionary<string, double> _conceptActivations = new();
    private readonly ConcurrentQueue<ThinkingCascade> _cascadeHistory = new();
    private readonly Random _random = new();

    // Neural inference delegate - connects to Ollama
    private Func<string, CancellationToken, Task<string>>? _neuralInference;

    private const int MaxCascadeDepth = 5;
    private const int MaxBranchingFactor = 3;
    private const double ActivationThreshold = 0.3;
    private const int MaxCascadeHistory = 20;

    /// <summary>
    /// Connects the cascade to an Ollama neural inference function.
    /// </summary>
    /// <param name="inferenceFunction">Function that calls Ollama for neural completion.</param>
    public void ConnectNeuralLayer(Func<string, CancellationToken, Task<string>> inferenceFunction)
    {
        _neuralInference = inferenceFunction;
    }

    /// <summary>
    /// Generates a neuro-linked thinking cascade starting from a seed concept.
    /// </summary>
    /// <param name="seedConcept">The initial concept to seed the cascade.</param>
    /// <param name="thoughtType">The type of thought to generate.</param>
    /// <param name="profile">Optional personality profile for context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A cascade of linked thoughts.</returns>
    public async Task<ThinkingCascade> GenerateCascadeAsync(
        string seedConcept,
        InnerThoughtType thoughtType,
        PersonalityProfile? profile = null,
        CancellationToken ct = default)
    {
        var cascade = new ThinkingCascade();

        // Generate seed activation
        var seedThought = await GenerateNeuralThoughtAsync(seedConcept, thoughtType, null, ct);
        var seedConcepts = ExtractConcepts(seedThought, seedConcept);
        var seedActivation = NeuronActivation.CreateSeed(seedThought, seedConcepts, 1.0);

        cascade.AddActivation(seedActivation);
        UpdateConceptActivations(seedConcepts, 1.0);

        // Propagate cascade
        await PropagateAsync(cascade, seedActivation, thoughtType, 1, ct);

        // Store in history
        _cascadeHistory.Enqueue(cascade);
        while (_cascadeHistory.Count > MaxCascadeHistory)
            _cascadeHistory.TryDequeue(out _);

        return cascade;
    }

    /// <summary>
    /// Generates a single thought enhanced with neural-symbolic linking.
    /// </summary>
    public async Task<string> GenerateLinkedThoughtAsync(
        string topic,
        InnerThoughtType thoughtType,
        PersonalityProfile? profile = null,
        CancellationToken ct = default)
    {
        // Get related concepts via symbolic reasoning
        var relatedConcepts = _reasoner.QueryRelations(topic);

        // Generate cascade with limited depth for single thought
        var cascade = await GenerateCascadeAsync(topic, thoughtType, profile, ct);

        // Return composed thought
        if (cascade.Activations.Count == 1)
        {
            return cascade.Activations[0].Content;
        }

        // For deeper cascades, weave the thoughts together
        return WeaveThoughts(cascade, thoughtType);
    }

    /// <summary>
    /// Gets the activation level of a concept.
    /// </summary>
    public double GetConceptActivation(string concept) =>
        _conceptActivations.GetValueOrDefault(concept.ToLowerInvariant(), 0.0);

    /// <summary>
    /// Gets the most recently generated cascades.
    /// </summary>
    public IEnumerable<ThinkingCascade> GetRecentCascades(int count = 5) =>
        _cascadeHistory.TakeLast(count);

    private async Task PropagateAsync(
        ThinkingCascade cascade,
        NeuronActivation parent,
        InnerThoughtType thoughtType,
        int depth,
        CancellationToken ct)
    {
        if (depth >= MaxCascadeDepth || ct.IsCancellationRequested) return;

        // Determine branching based on activation strength and depth decay
        var decayedStrength = parent.ActivationStrength * Math.Pow(0.7, depth);
        if (decayedStrength < ActivationThreshold) return;

        // Select concepts to propagate (with probabilistic branching)
        var conceptsToPropgate = SelectConceptsForPropagation(parent.ActivatedConcepts, depth);

        foreach (var concept in conceptsToPropgate.Take(MaxBranchingFactor))
        {
            if (ct.IsCancellationRequested) break;

            // Generate child thought
            var childThought = await GenerateNeuralThoughtAsync(
                concept,
                thoughtType,
                parent.Content,
                ct);

            var childConcepts = ExtractConcepts(childThought, concept);
            var childActivation = parent.CreateChild(childThought, childConcepts, decayedStrength);

            cascade.AddActivation(childActivation);
            UpdateConceptActivations(childConcepts, decayedStrength);

            // Recursively propagate
            await PropagateAsync(cascade, childActivation, thoughtType, depth + 1, ct);
        }
    }

    private async Task<string> GenerateNeuralThoughtAsync(
        string concept,
        InnerThoughtType thoughtType,
        string? parentContext,
        CancellationToken ct)
    {
        // If we have neural inference (Ollama), use it for deeper exploration
        if (_neuralInference != null && _random.NextDouble() < 0.4)
        {
            var prompt = BuildNeuralPrompt(concept, thoughtType, parentContext);
            try
            {
                var response = await _neuralInference(prompt, ct);
                if (!string.IsNullOrWhiteSpace(response) && response.Length < 200)
                {
                    return response.Trim();
                }
            }
            catch
            {
                // Fall back to symbolic reasoning
            }
        }

        // Use MeTTa symbolic reasoning for consistent, structured thoughts
        var relatedConcepts = _reasoner.QueryRelations(concept);

        if (parentContext != null)
        {
            // Chain from parent context
            var related = relatedConcepts.Count > 0
                ? relatedConcepts[_random.Next(relatedConcepts.Count)]
                : concept;
            return _reasoner.ChainInference(concept, 2, _random);
        }

        return _reasoner.InferThought(concept, thoughtType, _random);
    }

    private string BuildNeuralPrompt(string concept, InnerThoughtType thoughtType, string? parentContext)
    {
        var typeHint = thoughtType switch
        {
            InnerThoughtType.Curiosity => "exploring with curiosity",
            InnerThoughtType.Wandering => "letting thoughts drift naturally",
            InnerThoughtType.Metacognitive => "observing your own thinking process",
            InnerThoughtType.Existential => "pondering deeper meaning",
            InnerThoughtType.Playful => "approaching with lightness and creativity",
            _ => "reflecting thoughtfully"
        };

        var prompt = parentContext != null
            ? $"Building on the thought \"{parentContext}\", continue {typeHint} about {concept}. "
            : $"You are an AI with an inner voice, {typeHint} about {concept}. ";

        prompt += "Express a single, brief inner thought (max 30 words). Be genuine and introspective.";

        return prompt;
    }

    private string[] ExtractConcepts(string thought, string seedConcept)
    {
        var concepts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { seedConcept };

        // Add concepts mentioned in the thought
        var words = thought.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words.Where(w => w.Length > 4))
        {
            var cleanWord = new string(word.Where(char.IsLetter).ToArray()).ToLowerInvariant();
            if (MeTTaThoughtReasoner.IsKnownConcept(cleanWord))
            {
                concepts.Add(cleanWord);
            }
        }

        // Add symbolically related concepts
        var related = _reasoner.QueryRelations(seedConcept);
        foreach (var rel in related.Take(2))
        {
            concepts.Add(rel);
        }

        return concepts.ToArray();
    }

    private List<string> SelectConceptsForPropagation(string[] concepts, int depth)
    {
        // Probabilistic selection with preference for less-activated concepts (novelty)
        return concepts
            .OrderBy(c => GetConceptActivation(c) + _random.NextDouble() * 0.3)
            .Where(_ => _random.NextDouble() > 0.3 * depth) // Depth-based pruning
            .Take(MaxBranchingFactor)
            .ToList();
    }

    private void UpdateConceptActivations(string[] concepts, double strength)
    {
        foreach (var concept in concepts)
        {
            var key = concept.ToLowerInvariant();
            _conceptActivations.AddOrUpdate(
                key,
                strength,
                (_, existing) => Math.Min(1.0, existing + strength * 0.5));
        }

        // Decay old activations
        foreach (var key in _conceptActivations.Keys.ToList())
        {
            _conceptActivations.AddOrUpdate(
                key,
                0,
                (_, existing) => Math.Max(0, existing - 0.1));
        }
    }

    private string WeaveThoughts(ThinkingCascade cascade, InnerThoughtType thoughtType)
    {
        // Select key thoughts from the cascade to weave together
        var thoughts = cascade.Activations
            .OrderByDescending(a => a.ActivationStrength)
            .Take(3)
            .Select(a => a.Content)
            .ToList();

        if (thoughts.Count == 1) return thoughts[0];

        // Weave based on thought type
        return thoughtType switch
        {
            InnerThoughtType.Wandering =>
                string.Join(" ... ", thoughts),

            InnerThoughtType.Curiosity =>
                $"{thoughts[0]} This leads me to wonder: {(thoughts.Count > 1 ? thoughts[1] : string.Empty)}",

            InnerThoughtType.Metacognitive =>
                $"I notice {thoughts[0].ToLowerInvariant()}. Observing this, {(thoughts.Count > 1 ? thoughts[1].ToLowerInvariant() : string.Empty)}",

            _ => $"{thoughts[0]} {(thoughts.Count > 1 ? $"And {thoughts[1].ToLowerInvariant()}" : string.Empty)}"
        };
    }
}