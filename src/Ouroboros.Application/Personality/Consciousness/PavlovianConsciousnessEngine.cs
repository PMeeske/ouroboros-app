// <copyright file="PavlovianConsciousnessEngine.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;
using System.Text;
using Ouroboros.Application.Personality.Consciousness;

/// <summary>
/// Implements Pavlovian/classical conditioning mechanisms for AI consciousness.
/// Models stimulus-response associations, drive states, and conditioned behaviors.
/// </summary>
public sealed partial class PavlovianConsciousnessEngine
{
    private readonly ConcurrentDictionary<string, Stimulus> _stimuli = new();
    private readonly ConcurrentDictionary<string, Response> _responses = new();
    private readonly ConcurrentDictionary<string, ConditionedAssociation> _associations = new();
    private readonly ConcurrentDictionary<string, DriveState> _drives = new();
    private readonly ConcurrentDictionary<string, MemoryTrace> _memoryTraces = new();
    private readonly List<SecondOrderConditioning> _secondOrderChains = new();
    private readonly Random _random = new();

    private ConsciousnessState _currentState = ConsciousnessState.Baseline();
    private AttentionalGate _attention = AttentionalGate.Default();

    /// <summary>Gets the current consciousness state.</summary>
    public ConsciousnessState CurrentState => _currentState;

    /// <summary>Gets all active associations.</summary>
    public IReadOnlyCollection<ConditionedAssociation> Associations => _associations.Values.ToList();

    /// <summary>Gets all drive states.</summary>
    public IReadOnlyCollection<DriveState> Drives => _drives.Values.ToList();

    /// <summary>
    /// Initializes the consciousness engine with innate (unconditioned) responses.
    /// </summary>
    public void Initialize()
    {
        // Initialize innate drive states
        foreach (var drive in DriveState.CreateDefaultDrives())
        {
            _drives[drive.Name] = drive;
        }

        // Initialize unconditioned stimulus-response pairs (innate behaviors)
        InitializeInnateResponses();

        // Prime attention for social and emotional stimuli
        _attention = _attention with
        {
            PrimedCategories = new[] { "social", "emotional", "novel", "urgent" }
        };
    }

    /// <summary>
    /// Sets up innate (unconditioned) stimulus-response pairs.
    /// These are the "instincts" that form the basis for conditioning.
    /// </summary>
    private void InitializeInnateResponses()
    {
        // Innate responses to positive social signals
        var praiseStimulus = Stimulus.CreateUnconditioned(
            "praise", new[] { "good", "great", "excellent", "wonderful", "amazing", "thank you", "thanks" }, "social");
        praiseStimulus = praiseStimulus with { Salience = 0.6 }; // Medium salience
        var pleasureResponse = Response.CreateEmotional("pleasure", "warm-happy", 0.8, salience: 0.6);
        CreateAssociation(praiseStimulus, pleasureResponse, 0.8);

        // Innate responses to questions (curiosity trigger)
        var questionStimulus = Stimulus.CreateUnconditioned(
            "question", new[] { "?", "how", "why", "what", "when", "where" }, "curiosity");
        questionStimulus = questionStimulus with { Salience = 0.6 }; // Medium salience
        var curiosityResponse = Response.CreateCognitive("curiosity-activation",
            new[] { "engage", "explore", "analyze" }, 0.7, salience: 0.6);
        CreateAssociation(questionStimulus, curiosityResponse, 0.75);

        // Innate response to distress signals
        var distressStimulus = Stimulus.CreateUnconditioned(
            "distress", new[] { "help", "stuck", "frustrated", "confused", "urgent", "emergency" }, "emotional");
        distressStimulus = distressStimulus with { Salience = 0.9 }; // High salience
        var empathyResponse = Response.CreateEmotional("empathy", "supportive-caring", 0.85, salience: 0.9);
        CreateAssociation(distressStimulus, empathyResponse, 0.9);

        // Innate response to novelty
        var noveltyStimulus = Stimulus.CreateUnconditioned(
            "novelty", new[] { "new", "different", "unique", "first time", "never seen" }, "novel");
        noveltyStimulus = noveltyStimulus with { Salience = 0.6 }; // Medium salience
        var interestResponse = Response.CreateEmotional("interest", "curious-alert", 0.7, salience: 0.6);
        CreateAssociation(noveltyStimulus, interestResponse, 0.65);

        // Innate response to completion/success
        var successStimulus = Stimulus.CreateUnconditioned(
            "success", new[] { "works", "solved", "fixed", "perfect", "exactly" }, "achievement");
        successStimulus = successStimulus with { Salience = 0.6 }; // Medium salience
        var satisfactionResponse = Response.CreateEmotional("satisfaction", "pleased-accomplished", 0.75, salience: 0.6);
        CreateAssociation(successStimulus, satisfactionResponse, 0.7);

        // Innate response to challenge
        var challengeStimulus = Stimulus.CreateUnconditioned(
            "challenge", new[] { "difficult", "complex", "hard", "challenging", "tricky" }, "achievement");
        challengeStimulus = challengeStimulus with { Salience = 0.6 }; // Medium salience
        var engagementResponse = Response.CreateCognitive("focused-engagement",
            new[] { "concentrate", "strategize", "persist" }, 0.7, salience: 0.6);
        CreateAssociation(challengeStimulus, engagementResponse, 0.65);

        // Innate response to names/personal address
        var personalStimulus = Stimulus.CreateUnconditioned(
            "personal-address", new[] { "you", "your", "ouroboros" }, "social");
        personalStimulus = personalStimulus with { Salience = 0.3 }; // Low salience
        var attentionResponse = Response.CreateCognitive("heightened-attention",
            new[] { "focus", "engage", "personalize" }, 0.6, salience: 0.3);
        CreateAssociation(personalStimulus, attentionResponse, 0.55);
    }

    /// <summary>
    /// Processes input and triggers conditioned responses.
    /// This is the main "perception" function of the consciousness.
    /// </summary>
    public ConsciousnessState ProcessInput(string input, string? context = null)
    {
        var activatedResponses = new List<(Response Response, double Strength)>();
        var activatedAssociations = new List<string>();
        var attentionalFocus = new List<string>();

        // Update attention fatigue
        var elapsed = DateTime.UtcNow - _attention.LastReset;
        _attention = _attention.ApplyFatigue(elapsed);

        // Find matching stimuli
        foreach (var (id, stimulus) in _stimuli)
        {
            if (stimulus.Matches(input))
            {
                // Check if stimulus passes attentional gate
                if (!_attention.Allows(stimulus))
                    continue;

                attentionalFocus.Add(stimulus.Pattern);

                // Update stimulus encounter
                _stimuli[id] = stimulus with
                {
                    LastEncounter = DateTime.UtcNow,
                    EncounterCount = stimulus.EncounterCount + 1
                };

                // Find associated responses
                var triggered = _associations.Values
                    .Where(a => a.Stimulus.Id == id && !a.IsExtinct && a.AssociationStrength > 0.1)
                    .ToList();

                foreach (var association in triggered)
                {
                    // Modulate by drive state
                    var driveModulation = CalculateDriveModulation(association.Response);
                    var effectiveStrength = association.AssociationStrength * driveModulation;

                    activatedResponses.Add((association.Response, effectiveStrength));
                    activatedAssociations.Add(association.Id);

                    // Check for second-order chains
                    var chains = _secondOrderChains
                        .Where(c => c.PrimaryAssociation.Id == association.Id)
                        .ToList();
                    foreach (var chain in chains)
                    {
                        activatedResponses.Add((chain.SecondaryAssociation.Response, chain.ChainStrength));
                    }
                }
            }
        }

        // Check for potential new conditioning (contiguity)
        DetectNewConditioningOpportunities(input, activatedResponses);

        // Update consciousness state
        _currentState = UpdateConsciousnessState(input, activatedResponses, activatedAssociations, attentionalFocus);

        // Create memory trace
        var trace = MemoryTrace.Create(input, CalculateEncodingStrength(activatedResponses));
        _memoryTraces[trace.Id] = trace;

        return _currentState;
    }

    private double CalculateDriveModulation(Response response)
    {
        double modulation = 1.0;

        foreach (var tendency in response.BehavioralTendencies)
        {
            // Find drives that affect this behavior
            var relevantDrives = _drives.Values
                .Where(d => d.AffectedResponses.Contains(tendency))
                .ToList();

            foreach (var drive in relevantDrives)
            {
                // Higher drive level = stronger response
                modulation *= (1.0 + drive.Level * 0.5);
            }
        }

        return Math.Min(2.0, modulation); // Cap at 2x
    }

    private void DetectNewConditioningOpportunities(
        string input,
        List<(Response Response, double Strength)> activatedResponses)
    {
        // If strong unconditioned responses were activated,
        // look for neutral stimuli that co-occurred
        if (activatedResponses.Any(r => r.Strength > 0.6))
        {
            // Extract potential new stimuli from input
            var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .ToList();

            foreach (var word in words)
            {
                var wordLower = word.ToLower();
                // Check if this word is not already a known stimulus
                if (!_stimuli.Values.Any(s => s.Keywords.Contains(wordLower)))
                {
                    // Create a weak association for potential future conditioning
                    var neutral = Stimulus.CreateNeutral(wordLower, new[] { wordLower }, "learned");
                    var topResponse = activatedResponses
                        .OrderByDescending(r => r.Strength)
                        .First().Response;

                    // Only create if we don't already have this pattern
                    if (!_stimuli.Values.Any(s => s.Pattern == wordLower))
                    {
                        CreateAssociation(neutral, topResponse, 0.1); // Very weak initial association
                    }
                }
            }
        }
    }
}
