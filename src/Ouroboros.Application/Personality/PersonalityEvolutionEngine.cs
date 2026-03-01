// <copyright file="PersonalityEvolutionEngine.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Evolves personality profiles using genetic algorithms and records
/// evolution steps in MeTTa for reasoning.
/// </summary>
public sealed class PersonalityEvolutionEngine
{
    private readonly IMeTTaEngine _mettaEngine;
    private readonly ConcurrentDictionary<string, PersonalityProfile> _profiles;
    private readonly ConcurrentDictionary<string, List<InteractionFeedback>> _feedbackHistory;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalityEvolutionEngine"/> class.
    /// </summary>
    public PersonalityEvolutionEngine(
        IMeTTaEngine mettaEngine,
        ConcurrentDictionary<string, PersonalityProfile> profiles,
        ConcurrentDictionary<string, List<InteractionFeedback>> feedbackHistory)
    {
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _feedbackHistory = feedbackHistory ?? throw new ArgumentNullException(nameof(feedbackHistory));
    }

    /// <summary>
    /// Records feedback from an interaction to improve future personality expression.
    /// </summary>
    public void RecordFeedback(string personaName, InteractionFeedback feedback)
    {
        var history = _feedbackHistory.GetOrAdd(personaName, _ => new List<InteractionFeedback>());

        lock (history)
        {
            history.Add(feedback);
            // Keep only last 100 interactions
            if (history.Count > 100)
                history.RemoveAt(0);
        }

        // Update curiosity drivers based on feedback
        if (_profiles.TryGetValue(personaName, out var profile) && feedback.TopicDiscussed != null)
        {
            UpdateCuriosityDrivers(profile, feedback);
        }
    }

    /// <summary>
    /// Evolves the personality using genetic algorithm based on accumulated feedback.
    /// </summary>
    public async Task<PersonalityProfile> EvolvePersonalityAsync(
        string personaName,
        CancellationToken ct = default)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            throw new InvalidOperationException($"Profile not found: {personaName}");

        if (!_feedbackHistory.TryGetValue(personaName, out var feedback) || feedback.Count < 5)
            return profile; // Need enough data to evolve

        // Create initial population from current profile with variations
        var initialPopulation = CreatePopulationFromProfile(profile, 20);

        // Set up genetic algorithm with proper gene type
        var fitness = new PersonalityFitness(feedback.TakeLast(20).ToList());
        var ga = new GeneticAlgorithm<PersonalityGene>(
            fitness,
            MutatePersonalityGene,
            mutationRate: 0.15,
            crossoverRate: 0.7,
            elitismRate: 0.2);

        // Evolve over a few generations
        var result = await ga.EvolveAsync(initialPopulation, generations: 5, ct);

        if (result.IsSuccess)
        {
            var best = (PersonalityChromosome)result.Value;
            // Update profile with evolved values
            var evolvedProfile = ApplyEvolution(profile, best);
            _profiles[personaName] = evolvedProfile;

            // Add MeTTa facts about the evolution
            await RecordEvolutionInMeTTaAsync(personaName, evolvedProfile, ct);

            return evolvedProfile;
        }

        return profile; // Return original if evolution failed
    }

    /// <summary>
    /// Mutates a personality gene for genetic algorithm.
    /// </summary>
    internal static PersonalityGene MutatePersonalityGene(PersonalityGene gene)
    {
        var random = new Random();
        double delta = (random.NextDouble() - 0.5) * 0.3;
        double newValue = Math.Clamp(gene.Value + delta, 0.0, 1.0);
        return new PersonalityGene(gene.Key, newValue);
    }

    /// <summary>
    /// Creates an initial population from a profile for genetic evolution.
    /// </summary>
    internal List<IChromosome<PersonalityGene>> CreatePopulationFromProfile(PersonalityProfile profile, int size)
    {
        var population = new List<IChromosome<PersonalityGene>>();

        // Helper to create genes from profile
        List<PersonalityGene> CreateGenes(
            Dictionary<string, double> traits,
            Dictionary<string, double> curiosity,
            double proactivity,
            double adaptability)
        {
            var genes = new List<PersonalityGene>();
            foreach (var (key, value) in traits)
                genes.Add(new PersonalityGene($"trait:{key}", value));
            foreach (var (key, value) in curiosity)
                genes.Add(new PersonalityGene($"curiosity:{key}", value));
            genes.Add(new PersonalityGene("proactivity", proactivity));
            genes.Add(new PersonalityGene("adaptability", adaptability));
            return genes;
        }

        // Add current profile as first member
        var baseTraits = profile.Traits.ToDictionary(t => t.Key, t => t.Value.Intensity);
        var baseCuriosity = profile.CuriosityDrivers.ToDictionary(d => d.Topic, d => d.Interest);

        population.Add(new PersonalityChromosome(
            CreateGenes(baseTraits, baseCuriosity, 0.6, profile.AdaptabilityScore)));

        // Generate variations
        for (int i = 1; i < size; i++)
        {
            var variedTraits = baseTraits.ToDictionary(
                t => t.Key,
                t => Math.Clamp(t.Value + (_random.NextDouble() - 0.5) * 0.3, 0.0, 1.0));

            var variedCuriosity = baseCuriosity.ToDictionary(
                c => c.Key,
                c => Math.Clamp(c.Value + (_random.NextDouble() - 0.5) * 0.4, 0.0, 1.0));

            double variedProactivity = Math.Clamp(0.6 + (_random.NextDouble() - 0.5) * 0.4, 0.0, 1.0);
            double variedAdaptability = Math.Clamp(profile.AdaptabilityScore + (_random.NextDouble() - 0.5) * 0.2, 0.0, 1.0);

            population.Add(new PersonalityChromosome(
                CreateGenes(variedTraits, variedCuriosity, variedProactivity, variedAdaptability)));
        }

        return population;
    }

    /// <summary>
    /// Applies evolved values from the best chromosome back to the profile.
    /// </summary>
    internal static PersonalityProfile ApplyEvolution(PersonalityProfile profile, PersonalityChromosome best)
    {
        var evolvedTraits = profile.Traits.ToDictionary(
            t => t.Key,
            t => t.Value with
            {
                Intensity = best.GetTrait(t.Key)
            });

        var evolvedDrivers = profile.CuriosityDrivers
            .Select(d => d with
            {
                Interest = best.GetCuriosity(d.Topic)
            })
            .ToList();

        return profile with
        {
            Traits = evolvedTraits,
            CuriosityDrivers = evolvedDrivers,
            AdaptabilityScore = best.Adaptability,
            InteractionCount = profile.InteractionCount + 1,
            LastEvolution = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Records an evolution step in MeTTa for reasoning.
    /// </summary>
    internal async Task RecordEvolutionInMeTTaAsync(string personaName, PersonalityProfile profile, CancellationToken ct)
    {
        var topTraits = profile.GetActiveTraits(3).ToList();
        var traitFact = $"(evolved-personality {personaName} ({string.Join(" ", topTraits.Select(t => t.Name))}) {profile.AdaptabilityScore:F2})";
        await _mettaEngine.AddFactAsync(traitFact, ct);

        foreach (var driver in profile.CuriosityDrivers.Where(d => d.Interest > 0.7))
        {
            var curiosityFact = $"(high-curiosity {personaName} \"{driver.Topic}\" {driver.Interest:F2})";
            await _mettaEngine.AddFactAsync(curiosityFact, ct);
        }
    }

    private void UpdateCuriosityDrivers(PersonalityProfile profile, InteractionFeedback feedback)
    {
        if (feedback.TopicDiscussed == null) return;

        var existing = profile.CuriosityDrivers
            .FirstOrDefault(d => d.Topic.Equals(feedback.TopicDiscussed, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            // Update existing driver
            int idx = profile.CuriosityDrivers.IndexOf(existing);
            double newInterest = existing.Interest + (feedback.EngagementLevel - 0.5) * 0.1;

            profile.CuriosityDrivers[idx] = existing with
            {
                Interest = Math.Clamp(newInterest, 0.0, 1.0),
                LastAsked = feedback.QuestionAsked != null ? DateTime.UtcNow : existing.LastAsked,
                AskCount = feedback.QuestionAsked != null ? existing.AskCount + 1 : existing.AskCount
            };
        }
        else if (feedback.EngagementLevel > 0.6)
        {
            // Add new curiosity driver for engaging topics
            profile.CuriosityDrivers.Add(new CuriosityDriver(
                feedback.TopicDiscussed,
                feedback.EngagementLevel,
                GenerateRelatedQuestions(feedback.TopicDiscussed),
                DateTime.UtcNow,
                0));
        }
    }

    private static string[] GenerateRelatedQuestions(string topic)
    {
        return new[]
        {
            $"What's your experience with {topic}?",
            $"What challenges have you faced with {topic}?",
            $"How did you first get into {topic}?",
            $"What would you like to learn more about regarding {topic}?",
            $"Have you seen any interesting developments in {topic} lately?"
        };
    }
}
