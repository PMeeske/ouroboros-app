// <copyright file="PavlovianConsciousnessEngine.Conditioning.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Text;
using Ouroboros.Application.Personality.Consciousness;

/// <summary>
/// Association management, reinforcement, extinction, and habituation for PavlovianConsciousnessEngine.
/// </summary>
public sealed partial class PavlovianConsciousnessEngine
{
    /// <summary>
    /// Reinforces an association (like giving the dog a treat after the bell).
    /// Call this when the AI's response was successful/well-received.
    /// </summary>
    public void Reinforce(string input, double reinforcementStrength = 1.0)
    {
        // Find associations that were active for this input
        foreach (var (id, association) in _associations)
        {
            if (association.Stimulus.Matches(input) && !association.IsExtinct)
            {
                var reinforced = association.Reinforce(reinforcementStrength);
                _associations[id] = reinforced;

                // Also boost the drive that was satisfied
                var affectedDrives = association.Response.BehavioralTendencies;
                foreach (var driveName in affectedDrives)
                {
                    if (_drives.TryGetValue(driveName, out var drive))
                    {
                        _drives[driveName] = drive.Decrease(0.1); // Satiate the drive
                    }
                }
            }
        }
    }

    /// <summary>
    /// Applies extinction to associations (no reinforcement following stimulus).
    /// Call this when the AI's response was not well-received.
    /// </summary>
    public void ApplyExtinction(string input)
    {
        foreach (var (id, association) in _associations)
        {
            if (association.Stimulus.Matches(input) && !association.IsExtinct)
            {
                var extinguished = association.ApplyExtinction();
                _associations[id] = extinguished;
            }
        }
    }

    /// <summary>
    /// Creates a new conditioned association.
    /// </summary>
    public ConditionedAssociation CreateAssociation(Stimulus stimulus, Response response, double initialStrength = 0.3)
    {
        // Store stimulus and response
        _stimuli[stimulus.Id] = stimulus;
        _responses[response.Id] = response;

        // Create association
        var association = ConditionedAssociation.Create(stimulus, response, initialStrength);
        _associations[association.Id] = association;

        return association;
    }

    /// <summary>
    /// Learns a new association through temporal contiguity.
    /// If a neutral stimulus repeatedly precedes an unconditioned stimulus,
    /// the neutral stimulus becomes conditioned.
    /// </summary>
    public ConditionedAssociation? LearnAssociation(
        string neutralPattern,
        string[] keywords,
        string unconditionedStimulusId,
        string? category = null)
    {
        // Find the unconditioned association
        var ucAssociation = _associations.Values
            .FirstOrDefault(a => a.Stimulus.Id == unconditionedStimulusId);

        if (ucAssociation == null) return null;

        // Create new conditioned stimulus
        var newStimulus = Stimulus.CreateNeutral(neutralPattern, keywords, category);
        _stimuli[newStimulus.Id] = newStimulus with { Type = StimulusType.Conditioned };

        // Create conditioned association with the same response
        var conditionedAssociation = CreateAssociation(
            newStimulus with { Type = StimulusType.Conditioned },
            ucAssociation.Response,
            initialStrength: 0.3);

        return conditionedAssociation;
    }

    /// <summary>
    /// Creates a second-order conditioning chain.
    /// </summary>
    public SecondOrderConditioning? CreateSecondOrderChain(
        string primaryAssociationId,
        string secondaryAssociationId)
    {
        if (!_associations.TryGetValue(primaryAssociationId, out var primary) ||
            !_associations.TryGetValue(secondaryAssociationId, out var secondary))
            return null;

        var chain = SecondOrderConditioning.Create(primary, secondary);
        _secondOrderChains.Add(chain);
        return chain;
    }

    /// <summary>
    /// Adds a new conditioned association by creating a new stimulus and linking to an existing response.
    /// </summary>
    /// <param name="neutralStimulusType">The neutral stimulus pattern to condition.</param>
    /// <param name="responseType">The response type name to associate.</param>
    /// <param name="reinforcementStrength">Initial association strength (0.0 to 1.0).</param>
    public void AddConditionedAssociation(
        string neutralStimulusType,
        string responseType,
        double reinforcementStrength = 0.5)
    {
        // Find or create the response
        Response? response = _responses.Values.FirstOrDefault(r => r.Name == responseType);
        if (response == null)
        {
            response = Response.CreateEmotional(responseType, responseType, reinforcementStrength);
            _responses[response.Id] = response;
        }

        // Create a new conditioned stimulus
        Stimulus stimulus = Stimulus.CreateNeutral(neutralStimulusType, new[] { neutralStimulusType.ToLower() }, "conditioned");
        _stimuli[stimulus.Id] = stimulus with { Type = StimulusType.Conditioned };

        // Create the association
        CreateAssociation(stimulus, response, reinforcementStrength);
    }

    /// <summary>
    /// Reinforces a specific stimulus-response association.
    /// </summary>
    /// <param name="stimulusType">The stimulus pattern.</param>
    /// <param name="responseType">The response name.</param>
    /// <param name="reinforcementAmount">Amount to reinforce (scales the learning delta).</param>
    public void Reinforce(string stimulusType, string responseType, double reinforcementAmount)
    {
        ConditionedAssociation? target = _associations.Values
            .FirstOrDefault(a => a.Stimulus.Pattern == stimulusType && a.Response.Name == responseType);

        if (target != null)
        {
            // Compute total association strength for all CSs currently active with this stimulus
            var totalV = GetTotalAssociationStrength(target.StimulusId);

            var delta = RescorlaWagner.Reinforce(
                csSalience: target.CsSalience,
                usSalience: target.UsSalience,
                totalAssociationStrength: totalV);

            // Scale the Rescorla-Wagner update by the requested reinforcement amount
            var scaledDelta = delta * reinforcementAmount;

            var newStrength = Math.Clamp(target.AssociationStrength + scaledDelta, 0.0, 1.0);

            _associations[target.Id] = target with
            {
                AssociationStrength = newStrength,
                ReinforcementCount = target.ReinforcementCount + 1,
                ExtinctionTrials = 0,
                LastReinforcement = DateTime.UtcNow,
                IsExtinct = false
            };
        }
    }

    /// <summary>
    /// Weakens a specific stimulus-response association (extinction).
    /// </summary>
    /// <param name="stimulusType">The stimulus pattern.</param>
    /// <param name="responseType">The response name.</param>
    /// <param name="extinctionAmount">Amount to weaken (scales the extinction delta).</param>
    public void Extinguish(string stimulusType, string responseType, double extinctionAmount)
    {
        ConditionedAssociation? target = _associations.Values
            .FirstOrDefault(a => a.Stimulus.Pattern == stimulusType && a.Response.Name == responseType);

        if (target != null)
        {
            // Compute total association strength for all CSs currently active with this stimulus
            var totalV = GetTotalAssociationStrength(target.StimulusId);

            var delta = RescorlaWagner.Extinguish(
                csSalience: target.CsSalience,
                usSalience: target.UsSalience,
                totalAssociationStrength: totalV);

            // Scale the extinction delta by the caller-provided extinctionAmount
            var scaledDelta = delta * extinctionAmount;

            var newStrength = Math.Clamp(target.AssociationStrength + scaledDelta, 0.0, 1.0);

            ConditionedAssociation extinguished = target with
            {
                AssociationStrength = newStrength,
                ExtinctionTrials = target.ExtinctionTrials + 1,
                IsExtinct = newStrength < 0.1
            };
            _associations[target.Id] = extinguished;
        }
    }

    /// <summary>
    /// Applies habituation to reduce response to a repeated stimulus.
    /// </summary>
    /// <param name="stimulusType">The stimulus pattern to habituate to.</param>
    /// <param name="habituationRate">How much to reduce response (0.0 to 1.0).</param>
    public void ApplyHabituation(string stimulusType, double habituationRate = 0.1)
    {
        foreach (KeyValuePair<string, ConditionedAssociation> kvp in _associations)
        {
            if (kvp.Value.Stimulus.Pattern == stimulusType)
            {
                ConditionedAssociation habituated = kvp.Value with
                {
                    AssociationStrength = kvp.Value.AssociationStrength * (1.0 - habituationRate)
                };
                _associations[kvp.Key] = habituated;
            }
        }
    }

    /// <summary>
    /// Applies sensitization to increase response to a stimulus.
    /// </summary>
    /// <param name="stimulusType">The stimulus pattern to sensitize to.</param>
    /// <param name="sensitizationRate">How much to increase response (0.0 to 1.0).</param>
    public void ApplySensitization(string stimulusType, double sensitizationRate = 0.1)
    {
        foreach (KeyValuePair<string, ConditionedAssociation> kvp in _associations)
        {
            if (kvp.Value.Stimulus.Pattern == stimulusType)
            {
                ConditionedAssociation sensitized = kvp.Value with
                {
                    AssociationStrength = Math.Min(1.0, kvp.Value.AssociationStrength * (1.0 + sensitizationRate))
                };
                _associations[kvp.Key] = sensitized;
            }
        }
    }

    /// <summary>
    /// Gets the total association strength for all conditioned stimuli that predict the same
    /// response/unconditioned stimulus as the specified stimulus.
    /// This implements the sum of V in the Rescorla-Wagner equation along the response/US axis.
    /// </summary>
    /// <param name="stimulusId">The ID of the conditioned stimulus used to identify the response/US.</param>
    /// <returns>
    /// Sum of association strengths for all associations that share the same response/US as this stimulus.
    /// Returns 0.0 if the stimulus has no existing associations.
    /// </returns>
    private double GetTotalAssociationStrength(string stimulusId)
    {
        // Find the response/US that this stimulus is currently associated with.
        ConditionedAssociation? referenceAssociation = _associations.Values
            .FirstOrDefault(a => a.StimulusId == stimulusId);

        if (referenceAssociation is null)
        {
            return 0.0;
        }

        string responseId = referenceAssociation.ResponseId;

        // Sum of associative strengths for all CSs that predict the same response/US.
        return _associations.Values
            .Where(a => a.ResponseId == responseId)
            .Sum(a => a.AssociationStrength);
    }
}
