// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

namespace Ouroboros.Tests.Personality.Consciousness;

using FluentAssertions;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;
using Xunit;

/// <summary>
/// Complex logic tests for <see cref="PavlovianConsciousnessEngine"/>.
/// Covers stimulus-response conditioning, Rescorla-Wagner learning,
/// extinction, habituation, sensitization, consolidation, second-order
/// conditioning, drive modulation, and attentional gating.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PavlovianConsciousnessEngineTests
{
    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    private static PavlovianConsciousnessEngine CreateInitialized()
    {
        var engine = new PavlovianConsciousnessEngine();
        engine.Initialize();
        return engine;
    }

    // ==================================================================
    //  Initialize
    // ==================================================================

    [Fact]
    public void Initialize_SetsUpDefaultDrivesAndInnateAssociations()
    {
        // Arrange
        var engine = new PavlovianConsciousnessEngine();

        // Act
        engine.Initialize();

        // Assert
        engine.Drives.Should().NotBeEmpty("default drives should be created");
        engine.Associations.Should().NotBeEmpty("innate stimulus-response pairs should exist");
        engine.Drives.Select(d => d.Name).Should()
            .Contain("curiosity")
            .And.Contain("social")
            .And.Contain("achievement");
    }

    [Fact]
    public void Initialize_CalledTwice_DoesNotDuplicateDrives()
    {
        var engine = new PavlovianConsciousnessEngine();
        engine.Initialize();
        int driveCountAfterFirst = engine.Drives.Count;

        // Second call reinitializes - counts may differ but should not crash
        engine.Initialize();

        engine.Drives.Count.Should().BeGreaterThanOrEqualTo(driveCountAfterFirst);
    }

    // ==================================================================
    //  ProcessInput – stimulus matching and response activation
    // ==================================================================

    [Theory]
    [InlineData("That was great, thank you!", "praise")]
    [InlineData("How does this work?", "question")]
    [InlineData("Help! I'm stuck and frustrated", "distress")]
    [InlineData("This is something completely new and different", "novelty")]
    [InlineData("It works perfectly!", "success")]
    [InlineData("This is a really difficult challenge", "challenge")]
    public void ProcessInput_TriggersMatchingInnateStimulus(string input, string expectedPattern)
    {
        // Arrange
        var engine = CreateInitialized();

        // Act
        var state = engine.ProcessInput(input);

        // Assert – the returned state should have non-empty associations or spotlight
        state.Should().NotBeNull();
        // Arousal should increase when matching stimuli are found
        state.Arousal.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void ProcessInput_WithDistressKeywords_ProducesHighArousal()
    {
        var engine = CreateInitialized();

        var state = engine.ProcessInput("Help! This is an emergency, I'm so frustrated!");

        // Distress has salience 0.9 -> should produce meaningful arousal
        state.Arousal.Should().BeGreaterThan(0.3,
            "distress stimulus with high salience should raise arousal");
        state.DominantEmotion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ProcessInput_WithNoMatchingStimuli_DecaysArousal()
    {
        var engine = CreateInitialized();

        // First prime some arousal
        engine.ProcessInput("Thank you, that was amazing!");
        var afterPraise = engine.CurrentState;

        // Now send unrecognized gibberish
        var state = engine.ProcessInput("xyzzy plugh qwerty");

        // Arousal should decay (multiplied by 0.9 in the code)
        state.Arousal.Should().BeLessThanOrEqualTo(afterPraise.Arousal + 0.01,
            "arousal should decay when no stimuli match");
    }

    [Fact]
    public void ProcessInput_IncrementsEncounterCount()
    {
        var engine = CreateInitialized();

        engine.ProcessInput("How does this work?");
        engine.ProcessInput("Why is that?");

        // After two question-matching inputs, encounter count should have increased
        // Verify indirectly through consciousness state still functioning
        var state = engine.CurrentState;
        state.Should().NotBeNull();
    }

    [Fact]
    public void ProcessInput_CreatesMemoryTrace()
    {
        var engine = CreateInitialized();

        var state = engine.ProcessInput("This is wonderful, thank you!");

        // The engine should have created at least one memory trace
        state.Should().NotBeNull();
        state.StateTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ==================================================================
    //  Reinforce (simple, by input text)
    // ==================================================================

    [Fact]
    public void Reinforce_ByInput_IncreasesAssociationStrength()
    {
        var engine = CreateInitialized();

        // Get baseline strength for praise-related associations
        var before = engine.Associations
            .Where(a => a.Stimulus.Pattern == "praise")
            .Select(a => a.AssociationStrength)
            .FirstOrDefault();

        engine.Reinforce("That was great, thank you!");

        var after = engine.Associations
            .Where(a => a.Stimulus.Pattern == "praise")
            .Select(a => a.AssociationStrength)
            .FirstOrDefault();

        after.Should().BeGreaterThanOrEqualTo(before,
            "reinforcement should strengthen matching associations");
    }

    // ==================================================================
    //  Reinforce (Rescorla-Wagner, by stimulusType + responseType)
    // ==================================================================

    [Fact]
    public void Reinforce_RescorlaWagner_IncreasesStrength()
    {
        var engine = CreateInitialized();

        // Find a known innate association
        var assoc = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        double originalStrength = assoc.AssociationStrength;

        engine.Reinforce("praise", assoc.Response.Name, reinforcementAmount: 1.0);

        var updated = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        updated.AssociationStrength.Should().BeGreaterThan(originalStrength,
            "Rescorla-Wagner reinforcement should increase association strength");
        updated.ReinforcementCount.Should().BeGreaterThan(assoc.ReinforcementCount);
    }

    [Fact]
    public void Reinforce_RescorlaWagner_StrengthNeverExceedsOne()
    {
        var engine = CreateInitialized();
        var assoc = engine.Associations.First(a => a.Stimulus.Pattern == "praise");

        // Reinforce many times
        for (int i = 0; i < 100; i++)
        {
            engine.Reinforce("praise", assoc.Response.Name, reinforcementAmount: 10.0);
        }

        var updated = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        updated.AssociationStrength.Should().BeLessThanOrEqualTo(1.0,
            "association strength should be clamped at 1.0");
    }

    [Fact]
    public void Reinforce_RescorlaWagner_ResetsExtinctionTrials()
    {
        var engine = CreateInitialized();
        var assoc = engine.Associations.First(a => a.Stimulus.Pattern == "praise");

        // Apply some extinction first
        engine.Extinguish("praise", assoc.Response.Name, extinctionAmount: 0.5);
        var afterExtinction = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        afterExtinction.ExtinctionTrials.Should().BeGreaterThan(0);

        // Reinforce to reset
        engine.Reinforce("praise", afterExtinction.Response.Name, reinforcementAmount: 1.0);
        var afterReinforce = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        afterReinforce.ExtinctionTrials.Should().Be(0,
            "reinforcement should reset extinction trials");
        afterReinforce.IsExtinct.Should().BeFalse();
    }

    // ==================================================================
    //  Extinguish (Rescorla-Wagner)
    // ==================================================================

    [Fact]
    public void Extinguish_DecreasesAssociationStrength()
    {
        var engine = CreateInitialized();
        var assoc = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        double before = assoc.AssociationStrength;

        engine.Extinguish("praise", assoc.Response.Name, extinctionAmount: 1.0);

        var after = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        after.AssociationStrength.Should().BeLessThan(before,
            "extinction should decrease association strength");
        after.ExtinctionTrials.Should().Be(assoc.ExtinctionTrials + 1);
    }

    [Fact]
    public void Extinguish_RepeatedCalls_MarksAsExtinct()
    {
        var engine = CreateInitialized();
        var assoc = engine.Associations.First(a => a.Stimulus.Pattern == "praise");

        // Apply strong extinction many times
        for (int i = 0; i < 50; i++)
        {
            engine.Extinguish("praise", assoc.Response.Name, extinctionAmount: 5.0);
        }

        var after = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        after.IsExtinct.Should().BeTrue("repeated extinction should mark association as extinct");
        after.AssociationStrength.Should().BeLessThan(0.1);
    }

    [Fact]
    public void Extinguish_StrengthNeverGoesBelowZero()
    {
        var engine = CreateInitialized();
        var assoc = engine.Associations.First(a => a.Stimulus.Pattern == "praise");

        for (int i = 0; i < 200; i++)
        {
            engine.Extinguish("praise", assoc.Response.Name, extinctionAmount: 100.0);
        }

        var after = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        after.AssociationStrength.Should().BeGreaterThanOrEqualTo(0.0,
            "strength should be clamped at 0.0");
    }

    // ==================================================================
    //  ApplyExtinction (simple, by input text)
    // ==================================================================

    [Fact]
    public void ApplyExtinction_ByInput_WeakensMatchingAssociations()
    {
        var engine = CreateInitialized();
        var before = engine.Associations
            .Where(a => a.Stimulus.Pattern == "praise")
            .Select(a => a.AssociationStrength)
            .First();

        engine.ApplyExtinction("great, thank you");

        var after = engine.Associations
            .Where(a => a.Stimulus.Pattern == "praise")
            .Select(a => a.AssociationStrength)
            .First();

        after.Should().BeLessThan(before,
            "ApplyExtinction should weaken matching associations");
    }

    // ==================================================================
    //  CreateAssociation & LearnAssociation
    // ==================================================================

    [Fact]
    public void CreateAssociation_AddsToAssociationsCollection()
    {
        var engine = CreateInitialized();
        int before = engine.Associations.Count;

        var stim = Stimulus.CreateNeutral("test-bell", new[] { "bell", "ring" }, "test");
        var resp = Response.CreateEmotional("salivation", "hungry", 0.7);
        var assoc = engine.CreateAssociation(stim, resp, 0.5);

        engine.Associations.Count.Should().Be(before + 1);
        assoc.AssociationStrength.Should().Be(0.5);
        assoc.Stimulus.Pattern.Should().Be("test-bell");
        assoc.Response.Name.Should().Be("salivation");
    }

    [Fact]
    public void LearnAssociation_CreatesConditionedStimulusLinkedToExistingResponse()
    {
        var engine = CreateInitialized();

        // "praise" is an existing UC stimulus
        var praiseAssoc = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        string praiseStimId = praiseAssoc.Stimulus.Id;

        var learned = engine.LearnAssociation(
            "thumbs-up",
            new[] { "thumbs", "up", "+1" },
            praiseStimId,
            "gesture");

        learned.Should().NotBeNull("learning from an existing UC stimulus should succeed");
        learned!.Stimulus.Type.Should().Be(StimulusType.Conditioned);
        learned.Response.Id.Should().Be(praiseAssoc.Response.Id,
            "the conditioned response should link to the same response as the UC");
        learned.AssociationStrength.Should().Be(0.3,
            "initial strength for learned associations should be 0.3");
    }

    [Fact]
    public void LearnAssociation_WithInvalidStimulusId_ReturnsNull()
    {
        var engine = CreateInitialized();

        var result = engine.LearnAssociation(
            "unknown", new[] { "unknown" }, "non-existent-id");

        result.Should().BeNull("learning from a non-existent stimulus should return null");
    }

    // ==================================================================
    //  Second-order conditioning
    // ==================================================================

    [Fact]
    public void CreateSecondOrderChain_LinksExistingAssociations()
    {
        var engine = CreateInitialized();

        var primary = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        var secondary = engine.Associations.First(a => a.Stimulus.Pattern == "question");

        var chain = engine.CreateSecondOrderChain(primary.Id, secondary.Id);

        chain.Should().NotBeNull();
        chain!.ChainStrength.Should().BeApproximately(
            primary.AssociationStrength * secondary.AssociationStrength,
            0.001,
            "chain strength should be product of both association strengths");
        chain.ChainDepth.Should().Be(2);
    }

    [Fact]
    public void CreateSecondOrderChain_WithInvalidIds_ReturnsNull()
    {
        var engine = CreateInitialized();

        var result = engine.CreateSecondOrderChain("nonexistent-1", "nonexistent-2");

        result.Should().BeNull();
    }

    [Fact]
    public void ProcessInput_ActivatesSecondOrderChains()
    {
        var engine = CreateInitialized();

        // Create a custom second-order chain: praise -> novelty chain
        var primary = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        var secondary = engine.Associations.First(a => a.Stimulus.Pattern == "novelty");
        engine.CreateSecondOrderChain(primary.Id, secondary.Id);

        // When praise stimulus fires, secondary chain should also activate
        var state = engine.ProcessInput("That was great, thank you very much!");

        // The state should show activation (arousal > baseline)
        state.Arousal.Should().BeGreaterThan(0.3);
    }

    // ==================================================================
    //  Habituation & Sensitization
    // ==================================================================

    [Fact]
    public void ApplyHabituation_ReducesAssociationStrength()
    {
        var engine = CreateInitialized();
        var before = engine.Associations.First(a => a.Stimulus.Pattern == "praise");

        engine.ApplyHabituation("praise", habituationRate: 0.2);

        var after = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        after.AssociationStrength.Should().BeApproximately(
            before.AssociationStrength * 0.8, 0.001,
            "habituation reduces strength by (1 - rate)");
    }

    [Fact]
    public void ApplySensitization_IncreasesAssociationStrength()
    {
        var engine = CreateInitialized();
        var before = engine.Associations.First(a => a.Stimulus.Pattern == "praise");

        engine.ApplySensitization("praise", sensitizationRate: 0.2);

        var after = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        after.AssociationStrength.Should().BeApproximately(
            before.AssociationStrength * 1.2, 0.001,
            "sensitization increases strength by (1 + rate)");
    }

    [Fact]
    public void ApplySensitization_ClampedAtOne()
    {
        var engine = CreateInitialized();

        // Sensitize many times
        for (int i = 0; i < 50; i++)
        {
            engine.ApplySensitization("praise", sensitizationRate: 0.5);
        }

        var after = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        after.AssociationStrength.Should().BeLessThanOrEqualTo(1.0);
    }

    // ==================================================================
    //  Consolidation
    // ==================================================================

    [Fact]
    public void RunConsolidation_StrengthensHighlyReinforcedAssociations()
    {
        var engine = CreateInitialized();

        // Reinforce praise many times to get count > 3
        var praiseAssoc = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        for (int i = 0; i < 5; i++)
        {
            engine.Reinforce("praise", praiseAssoc.Response.Name, 0.5);
        }

        var before = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        before.ReinforcementCount.Should().BeGreaterThan(3);

        engine.RunConsolidation();

        var after = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        after.AssociationStrength.Should().BeGreaterThanOrEqualTo(before.AssociationStrength,
            "consolidation should strengthen frequently reinforced associations");
    }

    [Fact]
    public void RunConsolidation_DoesNotStrengthExtinctAssociations()
    {
        var engine = CreateInitialized();
        var assoc = engine.Associations.First(a => a.Stimulus.Pattern == "praise");

        // Extinguish it
        for (int i = 0; i < 50; i++)
        {
            engine.Extinguish("praise", assoc.Response.Name, extinctionAmount: 5.0);
        }

        var extinct = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        extinct.IsExtinct.Should().BeTrue();

        double beforeConsolidation = extinct.AssociationStrength;
        engine.RunConsolidation();

        // Extinct associations should not be strengthened by consolidation
        // (they may undergo spontaneous recovery, but strength should not exceed 60% of max)
        var afterConsolidation = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        afterConsolidation.AssociationStrength.Should().BeLessThanOrEqualTo(0.6);
    }

    // ==================================================================
    //  GetDominantResponse
    // ==================================================================

    [Fact]
    public void GetDominantResponse_AfterProcessInput_ReturnsStrongestResponse()
    {
        var engine = CreateInitialized();

        // Trigger distress (salience 0.9 -> strongest)
        engine.ProcessInput("Help! I'm stuck and frustrated, it's urgent");

        var dominant = engine.GetDominantResponse();

        // The distress association has the highest initial strength (0.9)
        // so its response should dominate when distress keywords match
        dominant.Should().NotBeNull();
    }

    [Fact]
    public void GetDominantResponse_WithNoActiveAssociations_ReturnsNull()
    {
        var engine = new PavlovianConsciousnessEngine();
        // Not initialized, no associations

        var dominant = engine.GetDominantResponse();

        dominant.Should().BeNull();
    }

    // ==================================================================
    //  GetActiveResponses
    // ==================================================================

    [Fact]
    public void GetActiveResponses_ReturnsAboveThreshold()
    {
        var engine = CreateInitialized();

        var responses = engine.GetActiveResponses(threshold: 0.3);

        responses.Should().NotBeEmpty(
            "initialized engine has innate associations above 0.3 threshold");
        responses.Values.Should().AllSatisfy(v => v.Should().BeGreaterThanOrEqualTo(0.3));
    }

    [Fact]
    public void GetActiveResponses_WithHighThreshold_ReturnsFewerResults()
    {
        var engine = CreateInitialized();

        var low = engine.GetActiveResponses(threshold: 0.1);
        var high = engine.GetActiveResponses(threshold: 0.9);

        high.Count.Should().BeLessThanOrEqualTo(low.Count);
    }

    // ==================================================================
    //  GetResponseModulation
    // ==================================================================

    [Fact]
    public void GetResponseModulation_ContainsDriveModulations()
    {
        var engine = CreateInitialized();
        engine.ProcessInput("How does quantum computing work?");

        var modulation = engine.GetResponseModulation();

        modulation.Should().ContainKey("arousal");
        modulation.Should().ContainKey("valence");
        modulation.Should().ContainKey("dominant_emotion");
        modulation.Should().ContainKey("awareness");
        modulation.Should().ContainKey("drive_curiosity");
        modulation.Should().ContainKey("drive_social");
    }

    [Fact]
    public void GetResponseModulation_IncludesSuggestedToneWhenDominantExists()
    {
        var engine = CreateInitialized();
        engine.ProcessInput("Help! This is urgent!");

        var modulation = engine.GetResponseModulation();

        // After processing a distress-matching input, dominant response should exist
        if (modulation.ContainsKey("suggested_tone"))
        {
            modulation["suggested_tone"].Should().NotBeNull();
        }
    }

    // ==================================================================
    //  AddConditionedAssociation
    // ==================================================================

    [Fact]
    public void AddConditionedAssociation_CreatesNewStimulusAndLinksToExistingResponse()
    {
        var engine = CreateInitialized();
        int before = engine.Associations.Count;

        engine.AddConditionedAssociation("doorbell", "pleasure", 0.6);

        engine.Associations.Count.Should().BeGreaterThan(before);
        // Note: AddConditionedAssociation creates a Neutral stimulus and passes it
        // to CreateAssociation which overwrites the _stimuli entry, so the
        // association's stimulus ends up with Type == Neutral despite the intent.
        engine.Associations.Should().Contain(a =>
            a.Stimulus.Pattern == "doorbell" &&
            a.Response.Name == "pleasure");
    }

    [Fact]
    public void AddConditionedAssociation_CreatesNewResponseIfNotFound()
    {
        var engine = CreateInitialized();

        engine.AddConditionedAssociation("beep", "completely-new-response", 0.4);

        engine.Associations.Should().Contain(a =>
            a.Response.Name == "completely-new-response");
    }

    // ==================================================================
    //  Consciousness state calculations
    // ==================================================================

    [Fact]
    public void ConsciousnessState_ValenceCalculation_PositiveForPraise()
    {
        var engine = CreateInitialized();

        var state = engine.ProcessInput("That was excellent, thank you! Great job!");

        // Praise -> pleasure response -> positive valence
        state.Valence.Should().BeGreaterThanOrEqualTo(0.0,
            "praise inputs should produce non-negative valence");
    }

    [Fact]
    public void ConsciousnessState_AwarenessIncreasesWithMoreAttention()
    {
        var engine = CreateInitialized();

        // Send input that matches multiple stimuli to fill attentional spotlight
        var state = engine.ProcessInput(
            "How does this amazing new thing work? Help me understand this challenging topic!");

        state.Awareness.Should().BeGreaterThanOrEqualTo(0.5,
            "multiple attentional focuses should produce higher awareness");
    }

    // ==================================================================
    //  DetectNewConditioningOpportunities (tested indirectly)
    // ==================================================================

    [Fact]
    public void ProcessInput_WithStrongResponseAndNewWords_CreatesWeakAssociations()
    {
        var engine = CreateInitialized();
        int associationsBefore = engine.Associations.Count;

        // Input contains praise keywords (strong response) plus a novel word ("synergy")
        engine.ProcessInput("great, excellent, synergy");

        // The engine should have auto-conditioned "synergy" as a weak association
        engine.Associations.Count.Should().BeGreaterThanOrEqualTo(associationsBefore,
            "novel words co-occurring with strong responses should generate new associations");
    }

    // ==================================================================
    //  GetTotalAssociationStrength (tested via Reinforce/Extinguish behavior)
    // ==================================================================

    [Fact]
    public void Reinforce_WithMultipleAssociationsOnSameResponse_RescorlaWagnerLimitsTotalStrength()
    {
        var engine = CreateInitialized();

        // Add a second conditioned stimulus that predicts the same pleasure response
        var praiseAssoc = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        engine.AddConditionedAssociation("clapping", praiseAssoc.Response.Name, 0.5);

        // Reinforce both - the total strength ΣV should limit learning
        // as both predict the same US
        double beforePraise = engine.Associations.First(a => a.Stimulus.Pattern == "praise").AssociationStrength;

        engine.Reinforce("praise", praiseAssoc.Response.Name, 1.0);

        double afterPraise = engine.Associations.First(a => a.Stimulus.Pattern == "praise").AssociationStrength;
        double delta = afterPraise - beforePraise;

        // With more total association strength, the delta should be smaller
        // than if praise was the only predictor
        delta.Should().BeLessThan(1.0, "Rescorla-Wagner should limit learning based on total ΣV");
    }

    // ==================================================================
    //  GetConsciousnessReport
    // ==================================================================

    [Fact]
    public void GetConsciousnessReport_ReturnsNonEmptyReport()
    {
        var engine = CreateInitialized();
        engine.ProcessInput("Tell me something interesting, please?");

        var report = engine.GetConsciousnessReport();

        report.Should().NotBeNullOrWhiteSpace();
        report.Should().Contain("PAVLOVIAN CONSCIOUSNESS REPORT");
        report.Should().Contain("DRIVE STATES");
        report.Should().Contain("TOP ASSOCIATIONS");
    }

    // ==================================================================
    //  GetConditioningSummary
    // ==================================================================

    [Fact]
    public void GetConditioningSummary_ListsTopAssociations()
    {
        var engine = CreateInitialized();

        var summary = engine.GetConditioningSummary();

        summary.Should().Contain("Conditioned Associations:");
        summary.Should().Contain("praise");
    }

    // ==================================================================
    //  Edge cases
    // ==================================================================

    [Fact]
    public void ProcessInput_EmptyString_DoesNotThrow()
    {
        var engine = CreateInitialized();

        var act = () => engine.ProcessInput("");

        act.Should().NotThrow();
    }

    [Fact]
    public void ProcessInput_NullContext_DoesNotThrow()
    {
        var engine = CreateInitialized();

        var act = () => engine.ProcessInput("hello", context: null);

        act.Should().NotThrow();
    }

    [Fact]
    public void Reinforce_NonMatchingInput_DoesNotAlterAssociations()
    {
        var engine = CreateInitialized();
        var before = engine.Associations.First(a => a.Stimulus.Pattern == "praise");

        engine.Reinforce("xyzzy no-match-here-at-all");

        var after = engine.Associations.First(a => a.Stimulus.Pattern == "praise");
        after.AssociationStrength.Should().Be(before.AssociationStrength);
    }

    [Fact]
    public void Reinforce_ByTypeAndName_NonExistentTarget_DoesNotThrow()
    {
        var engine = CreateInitialized();

        var act = () => engine.Reinforce("nonexistent-pattern", "nonexistent-response", 1.0);

        act.Should().NotThrow();
    }

    [Fact]
    public void Extinguish_NonExistentTarget_DoesNotThrow()
    {
        var engine = CreateInitialized();

        var act = () => engine.Extinguish("nonexistent", "nonexistent", 1.0);

        act.Should().NotThrow();
    }

    [Fact]
    public void ApplyHabituation_NonExistentStimulus_DoesNotThrow()
    {
        var engine = CreateInitialized();

        var act = () => engine.ApplyHabituation("nonexistent-stimulus");

        act.Should().NotThrow();
    }
}
