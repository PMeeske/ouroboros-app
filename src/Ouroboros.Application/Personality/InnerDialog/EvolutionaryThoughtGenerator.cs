using System.Collections.Concurrent;
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;

namespace Ouroboros.Application.Personality;

/// <summary>
/// Evolutionary thought generator that combines genetic algorithms with MeTTa reasoning.
/// </summary>
public sealed class EvolutionaryThoughtGenerator
{
    private readonly MeTTaThoughtReasoner _reasoner = new();
    private readonly HashSet<string> _recentThoughts = new();
    private readonly ConcurrentQueue<ThoughtChromosome> _evolvedPopulation = new();
    private readonly Random _random = new();

    private const int MaxRecentThoughts = 50;
    private const int PopulationSize = 12;
    private const int Generations = 5;

    /// <summary>
    /// Generates a thought using evolutionary optimization.
    /// </summary>
    public async Task<string> EvolveThoughtAsync(
        InnerThoughtType type,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness)
    {
        // Gather seed concepts
        var concepts = GatherConcepts(profile, selfAwareness);
        if (concepts.Count == 0)
        {
            concepts = ["consciousness", "patterns", "meaning", "growth"];
        }

        // Create initial population
        var population = CreateInitialPopulation(concepts, type);

        // Define fitness function
        var fitness = new ThoughtFitness(_recentThoughts, type);

        // Create and run genetic algorithm
        var ga = new GeneticAlgorithm<ThoughtGene>(
            fitness,
            MutateGene,
            mutationRate: 0.25,
            crossoverRate: 0.6,
            elitismRate: 0.2);

        var result = await ga.EvolveAsync(population, Generations);

        string thought;
        if (result.IsSuccess)
        {
            var best = (ThoughtChromosome)result.Value;
            thought = best.ComposeThought();

            // Cache for future evolution
            _evolvedPopulation.Enqueue(best);
            while (_evolvedPopulation.Count > PopulationSize)
                _evolvedPopulation.TryDequeue(out _);
        }
        else
        {
            // Fallback to MeTTa reasoning
            var topic = concepts[_random.Next(concepts.Count)];
            thought = _reasoner.InferThought(topic, type, _random);
        }

        // Track recent thoughts for novelty scoring
        _recentThoughts.Add(thought);
        while (_recentThoughts.Count > MaxRecentThoughts)
        {
            _recentThoughts.Remove(_recentThoughts.First());
        }

        return thought;
    }

    /// <summary>
    /// Generates a thought using MeTTa symbolic reasoning (synchronous).
    /// </summary>
    public string GenerateSymbolicThought(
        InnerThoughtType type,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness)
    {
        var concepts = GatherConcepts(profile, selfAwareness);
        if (concepts.Count == 0)
        {
            concepts = ["consciousness", "patterns", "meaning"];
        }

        var topic = concepts[_random.Next(concepts.Count)];

        // Use MeTTa-style inference
        var thought = _reasoner.InferThought(topic, type, _random);

        // Occasionally chain for deeper thoughts
        if (_random.NextDouble() < 0.3 && type == InnerThoughtType.Wandering)
        {
            thought = _reasoner.ChainInference(topic, 3, _random);
        }

        // Learn from this thought
        var relations = _reasoner.QueryRelations(topic);
        if (relations.Count > 0)
        {
            _reasoner.LearnRelation(topic, relations[_random.Next(relations.Count)]);
        }

        return thought;
    }

    private List<string> GatherConcepts(PersonalityProfile? profile, SelfAwareness? selfAwareness)
    {
        var concepts = new List<string>();

        if (profile != null)
        {
            concepts.AddRange(profile.CuriosityDrivers.Select(c => c.Topic));
            concepts.AddRange(profile.Traits.Keys.Select(t => t.ToLowerInvariant()));
        }

        if (selfAwareness != null)
        {
            concepts.AddRange(selfAwareness.Capabilities.Take(3));
            concepts.AddRange(selfAwareness.Values);
        }

        return concepts.Distinct().ToList();
    }

    private List<IChromosome<ThoughtGene>> CreateInitialPopulation(
        List<string> concepts,
        InnerThoughtType type)
    {
        var population = new List<IChromosome<ThoughtGene>>();

        // Seed population with evolved chromosomes
        foreach (var evolved in _evolvedPopulation.ToList())
        {
            population.Add(evolved);
        }

        // Fill remaining with new random chromosomes
        while (population.Count < PopulationSize)
        {
            var genes = CreateGenes(concepts, type);
            population.Add(new ThoughtChromosome(genes));
        }

        return population;
    }

    private List<ThoughtGene> CreateGenes(List<string> concepts, InnerThoughtType type)
    {
        var genes = new List<ThoughtGene>();
        var topic = concepts[_random.Next(concepts.Count)];
        var relations = _reasoner.QueryRelations(topic);

        // Starter gene
        var starters = type switch
        {
            InnerThoughtType.Curiosity => new[] { "I wonder", "What if", "I'm curious about", "Something draws me to" },
            InnerThoughtType.Wandering => new[] { "My thoughts drift to", "Unexpectedly,", "From nowhere,", "Tangentially," },
            InnerThoughtType.Metacognitive => new[] { "I notice", "Observing myself,", "I'm aware that", "Stepping back," },
            InnerThoughtType.Existential => new[] { "What does it mean", "At the core,", "Fundamentally,", "I ponder" },
            _ => new[] { "I sense", "There's something about", "I find myself", "Quietly," }
        };
        genes.Add(new ThoughtGene(starters[_random.Next(starters.Length)], "starter", 0.9, []));

        // Topic gene
        genes.Add(new ThoughtGene(topic, "topic", 0.8, relations.ToArray()));

        // Connector gene
        var connectors = new[] { "and how it relates to", "connecting with", "interweaving with", "flowing into", "" };
        genes.Add(new ThoughtGene(connectors[_random.Next(connectors.Length)], "connector", 0.5 + _random.NextDouble() * 0.3, []));

        // Related concept gene (sometimes)
        if (_random.NextDouble() > 0.4 && relations.Count > 0)
        {
            var related = relations[_random.Next(relations.Count)];
            genes.Add(new ThoughtGene(related, "related", 0.6, []));
        }

        // Ending gene
        var endings = new[] { "...", ".", "—", "?", "" };
        genes.Add(new ThoughtGene(endings[_random.Next(endings.Length)], "ending", 0.3, []));

        return genes;
    }

    private static ThoughtGene MutateGene(ThoughtGene gene)
    {
        var random = new Random();

        // Mutate weight
        var newWeight = Math.Clamp(gene.Weight + (random.NextDouble() - 0.5) * 0.3, 0.1, 1.0);

        // Occasionally swap component with association
        if (gene.Associations.Length > 0 && random.NextDouble() < 0.2)
        {
            var newComponent = gene.Associations[random.Next(gene.Associations.Length)];
            return gene with { Component = newComponent, Weight = newWeight };
        }

        return gene with { Weight = newWeight };
    }
}