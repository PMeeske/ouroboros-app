using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Application.Personality;

/// <summary>
/// Fitness function for personality evolution based on interaction success.
/// </summary>
public sealed class PersonalityFitness : IFitnessFunction<PersonalityGene>
{
    private readonly List<InteractionFeedback> _recentFeedback;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalityFitness"/> class.
    /// </summary>
    public PersonalityFitness(List<InteractionFeedback> recentFeedback)
    {
        _recentFeedback = recentFeedback;
    }

    /// <inheritdoc/>
    public Task<double> EvaluateAsync(IChromosome<PersonalityGene> chromosome, CancellationToken cancellationToken)
    {
        if (_recentFeedback.Count == 0)
            return Task.FromResult(0.5);

        PersonalityChromosome pc = (PersonalityChromosome)chromosome;

        double engagementScore = _recentFeedback.Average(f => f.EngagementLevel);
        double relevanceScore = _recentFeedback.Average(f => f.ResponseRelevance);
        double questionScore = _recentFeedback.Average(f => f.QuestionQuality);
        double continuityScore = _recentFeedback.Average(f => f.ConversationContinuity);

        // Weight proactivity more if questions led to good engagement
        double proactivityBonus = pc.ProactivityLevel * questionScore;

        double fitness = (engagementScore * 0.3 +
                          relevanceScore * 0.25 +
                          questionScore * 0.2 +
                          continuityScore * 0.15 +
                          proactivityBonus * 0.1);

        return Task.FromResult(fitness);
    }
}