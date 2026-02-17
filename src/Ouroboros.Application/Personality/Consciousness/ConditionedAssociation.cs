namespace Ouroboros.Application.Personality;

/// <summary>
/// An association between a stimulus and a response - the core of conditioning.
/// Implements Rescorla-Wagner learning model for association strength.
/// </summary>
public sealed record ConditionedAssociation(
    string Id,
    Stimulus Stimulus,
    Response Response,
    double AssociationStrength,  // 0-1: strength of the S-R link (V in Rescorla-Wagner)
    double CsSalience,           // α: salience of conditioned stimulus (from Stimulus.Salience)
    double UsSalience,           // β: salience of unconditioned stimulus (from Response.Salience)
    double LearningRate,         // Deprecated: kept for backward compatibility, use PavlovianConsciousnessEngine methods
    double MaxStrength,          // λ: maximum possible association strength
    int ReinforcementCount,      // Number of times this association was reinforced
    int ExtinctionTrials,        // Number of non-reinforced trials (for extinction)
    DateTime LastReinforcement,
    DateTime Created,
    bool IsExtinct)              // Whether this association has been extinguished
{
    /// <summary>
    /// Gets the stimulus ID for efficient lookups.
    /// </summary>
    public string StimulusId => Stimulus.Id;

    /// <summary>
    /// Gets the response ID for efficient lookups.
    /// </summary>
    public string ResponseId => Response.Id;

    /// <summary>
    /// Updates association strength using a simplified learning equation.
    /// NOTE: For Rescorla-Wagner model behavior, use PavlovianConsciousnessEngine methods instead.
    /// This method is maintained for backward compatibility.
    /// </summary>
    public ConditionedAssociation Reinforce(double reinforcementStrength = 1.0)
    {
        double effectiveMax = MaxStrength * reinforcementStrength;
        double deltaV = LearningRate * (effectiveMax - AssociationStrength);
        double newStrength = Math.Min(1.0, Math.Max(0.0, AssociationStrength + deltaV));

        return this with
        {
            AssociationStrength = newStrength,
            ReinforcementCount = ReinforcementCount + 1,
            ExtinctionTrials = 0, // Reset extinction counter
            LastReinforcement = DateTime.UtcNow,
            IsExtinct = false
        };
    }

    /// <summary>
    /// Applies extinction - weakens association when stimulus occurs without reinforcement.
    /// </summary>
    public ConditionedAssociation ApplyExtinction(double extinctionRate = 0.1)
    {
        double newStrength = AssociationStrength * (1.0 - extinctionRate);
        int newExtinctionTrials = ExtinctionTrials + 1;
        bool isExtinct = newStrength < 0.1;

        return this with
        {
            AssociationStrength = newStrength,
            ExtinctionTrials = newExtinctionTrials,
            IsExtinct = isExtinct
        };
    }

    /// <summary>
    /// Applies spontaneous recovery - partial return of extinguished association after time.
    /// </summary>
    public ConditionedAssociation ApplySpontaneousRecovery(TimeSpan timeSinceExtinction)
    {
        if (!IsExtinct) return this;

        // Recovery is proportional to log of time passed
        double hoursPassed = timeSinceExtinction.TotalHours;
        double recoveryFactor = Math.Min(0.5, Math.Log(1 + hoursPassed) * 0.1);
        double recoveredStrength = AssociationStrength + (MaxStrength * recoveryFactor);

        return this with
        {
            AssociationStrength = Math.Min(MaxStrength * 0.6, recoveredStrength),
            IsExtinct = recoveredStrength > 0.15
        };
    }

    /// <summary>Creates a new association with default learning parameters.</summary>
    public static ConditionedAssociation Create(Stimulus stimulus, Response response, double initialStrength = 0.3) => new(
        Id: Guid.NewGuid().ToString(),
        Stimulus: stimulus,
        Response: response,
        AssociationStrength: initialStrength,
        CsSalience: stimulus.Salience,
        UsSalience: response.Salience,
        LearningRate: 0.2,      // Moderate learning rate
        MaxStrength: 1.0,
        ReinforcementCount: 1,
        ExtinctionTrials: 0,
        LastReinforcement: DateTime.UtcNow,
        Created: DateTime.UtcNow,
        IsExtinct: false);
}