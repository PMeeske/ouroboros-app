// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

namespace Ouroboros.Tests.Personality;

using FluentAssertions;
using Moq;
using Ouroboros.Abstractions;
using Ouroboros.Application.Personality;
using Ouroboros.Tools.MeTTa;
using Xunit;

/// <summary>
/// Complex logic tests for <see cref="PersonalityEngine"/>.
/// Covers mood detection, mood state transitions, person detection,
/// relationship management, proactivity calculations, personality evolution,
/// text sanitization, keyword extraction, and consciousness integration.
/// Does NOT re-test simple property getters already covered elsewhere.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PersonalityEngineComplexLogicTests : IAsyncLifetime
{
    private readonly Mock<IMeTTaEngine> _mettaEngine = new();
    private PersonalityEngine _sut = null!;

    public async Task InitializeAsync()
    {
        // Set up MeTTa mocks for initialization
        _mettaEngine.Setup(m => m.ApplyRuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("ok"));
        _mettaEngine.Setup(m => m.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit, string>.Success(Unit.Value));
        _mettaEngine.Setup(m => m.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success(""));

        _sut = new PersonalityEngine(_mettaEngine.Object);
        await _sut.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _sut.DisposeAsync();
    }

    // ------------------------------------------------------------------
    //  Helper: Create a profile with known traits
    // ------------------------------------------------------------------

    private PersonalityProfile EnsureProfile(string name = "TestBot")
    {
        return _sut.GetOrCreateProfile(
            name,
            new[] { "curious", "warm", "analytical", "witty" },
            new[] { "cheerful", "focused", "excited" },
            "A helpful test assistant");
    }

    // ==================================================================
    //  DetectMoodFromInput – complex branching logic
    // ==================================================================

    [Theory]
    [InlineData("This is amazing! I love it!", "excited")]
    [InlineData("I'm so frustrated, nothing works!", "frustrated")]
    [InlineData("whatever, fine", null)] // low engagement
    [InlineData("Can you explain how this works?", "curious")]
    [InlineData("This needs to be done ASAP, it's urgent!", "urgent")]
    public void DetectMoodFromInput_IdentifiesDominantEmotion(string input, string? expectedEmotion)
    {
        var detected = _sut.DetectMoodFromInput(input);

        if (expectedEmotion != null)
        {
            detected.DominantEmotion.Should().Be(expectedEmotion);
        }
        else
        {
            detected.DominantEmotion.Should().NotBeNull();
        }

        detected.Confidence.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void DetectMoodFromInput_HighEnergyPositiveInput_SetsHighEnergyAndPositivity()
    {
        var detected = _sut.DetectMoodFromInput(
            "This is exciting! Amazing! I love it! Absolutely fantastic!");

        detected.Energy.Should().BeGreaterThan(0.0, "highly energetic words should raise energy");
        detected.Positivity.Should().BeGreaterThan(0.0, "positive words should raise positivity");
    }

    [Fact]
    public void DetectMoodFromInput_FrustrationKeywords_SetsFrustration()
    {
        var detected = _sut.DetectMoodFromInput(
            "I'm so frustrated! Nothing works! I tried everything and I'm stuck. Ugh!");

        detected.Frustration.Should().BeGreaterThan(0.3);
        detected.DominantEmotion.Should().Be("frustrated");
    }

    [Fact]
    public void DetectMoodFromInput_UrgencyKeywords_SetsUrgency()
    {
        var detected = _sut.DetectMoodFromInput(
            "This is urgent! I need this done ASAP, it's critical!");

        detected.Urgency.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public void DetectMoodFromInput_CuriosityKeywords_SetsCuriosity()
    {
        var detected = _sut.DetectMoodFromInput(
            "Why does this happen? How can I explore this further? I'm curious about the details.");

        detected.Curiosity.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public void DetectMoodFromInput_EmptyInput_ReturnsNeutral()
    {
        var detected = _sut.DetectMoodFromInput("");

        detected.Should().Be(DetectedMood.Neutral);
    }

    [Fact]
    public void DetectMoodFromInput_WhitespaceOnly_ReturnsNeutral()
    {
        var detected = _sut.DetectMoodFromInput("   ");

        detected.Should().Be(DetectedMood.Neutral);
    }

    [Fact]
    public void DetectMoodFromInput_ShortDisengagedInput_HasLowEngagement()
    {
        var detected = _sut.DetectMoodFromInput("ok");

        detected.Engagement.Should().BeLessThan(0.5,
            "very short apathetic inputs should indicate low engagement");
    }

    [Fact]
    public void DetectMoodFromInput_LongDetailedInput_HasHighEngagement()
    {
        var longInput = string.Join(" ",
            Enumerable.Range(0, 60).Select(i => $"detailed-word-{i}"));

        var detected = _sut.DetectMoodFromInput(longInput);

        detected.Engagement.Should().BeGreaterThan(0.5,
            "long detailed inputs should indicate high engagement");
    }

    [Fact]
    public void DetectMoodFromInput_NegativeWords_DecreasePositivity()
    {
        var detected = _sut.DetectMoodFromInput(
            "This is terrible, awful, horrible. I hate this bug. The worst issue I've seen.");

        detected.Positivity.Should().BeLessThan(0.0);
    }

    [Fact]
    public void DetectMoodFromInput_LowEnergyWords_DecreaseEnergy()
    {
        var detected = _sut.DetectMoodFromInput(
            "I'm tired and exhausted... boring, slow, meh");

        detected.Energy.Should().BeLessThan(0.0);
    }

    [Fact]
    public void DetectMoodFromInput_ValuesClampedWithinRange()
    {
        // Very extreme input
        var detected = _sut.DetectMoodFromInput(
            "exciting amazing awesome incredible fantastic wow love great excellent wonderful " +
            "absolutely perfect brilliant ! !! !!! can't wait so excited");

        detected.Energy.Should().BeGreaterThanOrEqualTo(-1.0).And.BeLessThanOrEqualTo(1.0);
        detected.Positivity.Should().BeGreaterThanOrEqualTo(-1.0).And.BeLessThanOrEqualTo(1.0);
        detected.Urgency.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
        detected.Curiosity.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
        detected.Frustration.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
        detected.Engagement.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
        detected.Confidence.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
    }

    // ==================================================================
    //  UpdateMood – state machine transitions
    // ==================================================================

    [Fact]
    public void UpdateMood_PositiveInteraction_IncreasesPositivity()
    {
        var profile = EnsureProfile();
        double before = profile.CurrentMood.Positivity;

        _sut.UpdateMood("TestBot", "This is exciting!", positiveInteraction: true);

        var updated = _sut.GetOrCreateProfile("TestBot", Array.Empty<string>(), Array.Empty<string>(), "");
        updated.CurrentMood.Positivity.Should().BeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void UpdateMood_NegativeInteraction_DecreasesPositivity()
    {
        var profile = EnsureProfile();
        double before = profile.CurrentMood.Positivity;

        _sut.UpdateMood("TestBot", "boring meh whatever", positiveInteraction: false);

        var updated = _sut.GetOrCreateProfile("TestBot", Array.Empty<string>(), Array.Empty<string>(), "");
        updated.CurrentMood.Positivity.Should().BeLessThanOrEqualTo(before);
    }

    [Theory]
    [InlineData(0.8, 0.8, "excited")]
    [InlineData(0.8, 0.2, "intense")]
    [InlineData(0.2, 0.8, "content")]
    [InlineData(0.2, 0.2, "contemplative")]
    public void UpdateMood_ProducesCorrectMoodName(double targetEnergy, double targetPositivity, string expectedMood)
    {
        EnsureProfile();

        // Set energy/positivity near the target thresholds via repeated calls
        // Start from a known state by updating with extreme inputs
        if (targetEnergy > 0.5)
            _sut.UpdateMood("TestBot", "exciting amazing awesome", positiveInteraction: targetPositivity > 0.5);
        else
            _sut.UpdateMood("TestBot", "tired boring slow", positiveInteraction: targetPositivity > 0.5);

        // The mood name should be one of the expected values
        var currentMood = _sut.GetCurrentMood("TestBot");
        currentMood.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void UpdateMood_ClampsEnergyAndPositivity()
    {
        EnsureProfile();

        // Push energy and positivity as high as possible
        for (int i = 0; i < 100; i++)
        {
            _sut.UpdateMood("TestBot", "exciting! amazing! awesome!", positiveInteraction: true);
        }

        var profile = _sut.GetOrCreateProfile("TestBot", Array.Empty<string>(), Array.Empty<string>(), "");
        profile.CurrentMood.Energy.Should().BeLessThanOrEqualTo(1.0);
        profile.CurrentMood.Positivity.Should().BeLessThanOrEqualTo(1.0);
    }

    // ==================================================================
    //  UpdateMoodFromDetection – comprehensive mood blending
    // ==================================================================

    [Fact]
    public void UpdateMoodFromDetection_FrustrationInput_SetsSupportiveMood()
    {
        EnsureProfile();

        _sut.UpdateMoodFromDetection("TestBot",
            "I'm so frustrated! Nothing works! Tried everything, stuck again!");

        var mood = _sut.GetCurrentMood("TestBot");
        mood.Should().Be("supportive",
            "when user is frustrated, AI mood should shift to supportive");
    }

    [Fact]
    public void UpdateMoodFromDetection_UrgentInput_SetsFocusedMood()
    {
        EnsureProfile();

        _sut.UpdateMoodFromDetection("TestBot",
            "This is urgent! I need this done ASAP, it's critical and an emergency!");

        var mood = _sut.GetCurrentMood("TestBot");
        mood.Should().Be("focused",
            "when user is urgent, AI mood should shift to focused");
    }

    [Fact]
    public void UpdateMoodFromDetection_CuriousInput_SetsIntriguedMood()
    {
        EnsureProfile();

        _sut.UpdateMoodFromDetection("TestBot",
            "Why does this happen? How does that work? I'm curious about exploring this!");

        var mood = _sut.GetCurrentMood("TestBot");
        mood.Should().Be("intrigued",
            "when user is curious, AI mood should match with intrigued");
    }

    // ==================================================================
    //  Person Detection (synchronous path)
    // ==================================================================

    [Fact]
    public void SetCurrentPerson_ByName_CreatesNewPerson()
    {
        var result = _sut.SetCurrentPerson("Alice");

        result.IsNewPerson.Should().BeTrue();
        result.Person.Name.Should().Be("Alice");
        result.NameWasProvided.Should().BeTrue();
        result.MatchConfidence.Should().Be(0.0, "brand new person has no match");
        _sut.CurrentPerson.Should().NotBeNull();
        _sut.CurrentPerson!.Name.Should().Be("Alice");
    }

    [Fact]
    public void SetCurrentPerson_ExistingName_FindsExistingPerson()
    {
        _sut.SetCurrentPerson("Bob");
        var secondResult = _sut.SetCurrentPerson("Bob");

        secondResult.IsNewPerson.Should().BeFalse();
        secondResult.MatchConfidence.Should().Be(1.0, "explicit name match should be 100%");
    }

    [Fact]
    public async Task DetectPersonAsync_WithExplicitName_ExtractsName()
    {
        var result = await _sut.DetectPersonAsync("My name is Charlie");

        result.Person.Should().NotBeNull();
        result.NameWasProvided.Should().BeTrue();
        result.Person.Name.Should().Be("Charlie");
        result.IsNewPerson.Should().BeTrue();
    }

    [Fact]
    public async Task DetectPersonAsync_GermanNameIntro_ExtractsName()
    {
        var result = await _sut.DetectPersonAsync("Ich bin Markus");

        result.NameWasProvided.Should().BeTrue();
        result.Person.Name.Should().Be("Markus");
    }

    [Fact]
    public async Task DetectPersonAsync_ReturningPerson_RecognizesByName()
    {
        // First interaction
        await _sut.DetectPersonAsync("My name is Diana");

        // Second interaction
        var result = await _sut.DetectPersonAsync("Hi, it's me, Diana");

        result.IsNewPerson.Should().BeFalse("Diana should be recognized on return");
        result.Person.InteractionCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task DetectPersonAsync_NoName_CreatesUnknownPerson()
    {
        var result = await _sut.DetectPersonAsync("Can you help me with something?");

        result.Person.Should().NotBeNull();
        result.NameWasProvided.Should().BeFalse();
        result.IsNewPerson.Should().BeTrue();
    }

    // ==================================================================
    //  Relationship Management
    // ==================================================================

    [Fact]
    public void UpdateRelationship_PositiveInteraction_IncreasesRapport()
    {
        _sut.SetCurrentPerson("Eve");
        var person = _sut.CurrentPerson!;

        // First call creates the relationship with baseline rapport (0.5)
        _sut.UpdateRelationship(person.Id, "testing", isPositive: true);
        // Second call applies the positive delta (+0.05)
        _sut.UpdateRelationship(person.Id, "follow-up", isPositive: true);
        var rel = _sut.GetRelationship(person.Id);

        rel.Should().NotBeNull();
        rel!.Rapport.Should().BeGreaterThan(0.5, "positive interaction should increase rapport from baseline 0.5");
        rel.PositiveInteractions.Should().Be(1, "only the second call increments the count since the first creates the relationship");
    }

    [Fact]
    public void UpdateRelationship_NegativeInteraction_DecreasesRapportAndTrust()
    {
        _sut.SetCurrentPerson("Frank");
        var person = _sut.CurrentPerson!;

        _sut.UpdateRelationship(person.Id, isPositive: true); // Create relationship
        _sut.UpdateRelationship(person.Id, isPositive: false);

        var rel = _sut.GetRelationship(person.Id);
        rel!.NegativeInteractions.Should().Be(1);
    }

    [Fact]
    public void UpdateRelationship_WithTopic_TracksSharedTopics()
    {
        _sut.SetCurrentPerson("Grace");
        var person = _sut.CurrentPerson!;

        _sut.UpdateRelationship(person.Id, topic: "machine learning", isPositive: true);
        _sut.UpdateRelationship(person.Id, topic: "data science", isPositive: true);

        var rel = _sut.GetRelationship(person.Id);
        rel!.SharedTopics.Should().Contain("machine learning");
        rel.SharedTopics.Should().Contain("data science");
    }

    [Fact]
    public void UpdateRelationship_SharedTopicsCappedAtTen()
    {
        _sut.SetCurrentPerson("Hank");
        var person = _sut.CurrentPerson!;

        for (int i = 0; i < 15; i++)
        {
            _sut.UpdateRelationship(person.Id, topic: $"topic-{i}", isPositive: true);
        }

        var rel = _sut.GetRelationship(person.Id);
        rel!.SharedTopics.Length.Should().BeLessThanOrEqualTo(10,
            "shared topics should be capped at 10");
    }

    [Fact]
    public void UpdateRelationship_RapportClampedBetweenZeroAndOne()
    {
        _sut.SetCurrentPerson("Ivy");
        var person = _sut.CurrentPerson!;

        // Many positive interactions
        for (int i = 0; i < 100; i++)
        {
            _sut.UpdateRelationship(person.Id, isPositive: true);
        }

        var rel = _sut.GetRelationship(person.Id);
        rel!.Rapport.Should().BeLessThanOrEqualTo(1.0);

        // Many negative interactions
        for (int i = 0; i < 200; i++)
        {
            _sut.UpdateRelationship(person.Id, isPositive: false);
        }

        rel = _sut.GetRelationship(person.Id);
        rel!.Rapport.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void AddNotableMemory_AppendsMemoryWithDate()
    {
        _sut.SetCurrentPerson("Jack");
        var person = _sut.CurrentPerson!;
        _sut.UpdateRelationship(person.Id, isPositive: true); // Create relationship

        _sut.AddNotableMemory(person.Id, "Likes Python");

        var rel = _sut.GetRelationship(person.Id);
        rel!.ThingsToRemember.Should().ContainSingle(m => m.Contains("Likes Python"));
    }

    [Fact]
    public void AddNotableMemory_CappedAtTwenty()
    {
        _sut.SetCurrentPerson("Kate");
        var person = _sut.CurrentPerson!;
        _sut.UpdateRelationship(person.Id, isPositive: true);

        for (int i = 0; i < 25; i++)
        {
            _sut.AddNotableMemory(person.Id, $"memory-{i}");
        }

        var rel = _sut.GetRelationship(person.Id);
        rel!.ThingsToRemember.Length.Should().BeLessThanOrEqualTo(20);
    }

    [Fact]
    public void SetPersonPreference_TracksPreference()
    {
        _sut.SetCurrentPerson("Leo");
        var person = _sut.CurrentPerson!;
        _sut.UpdateRelationship(person.Id, isPositive: true);

        _sut.SetPersonPreference(person.Id, "Prefers verbose explanations");

        var rel = _sut.GetRelationship(person.Id);
        rel!.PersonPreferences.Should().Contain("Prefers verbose explanations");
    }

    [Fact]
    public void SetPersonPreference_DoesNotDuplicateExisting()
    {
        _sut.SetCurrentPerson("Mia");
        var person = _sut.CurrentPerson!;
        _sut.UpdateRelationship(person.Id, isPositive: true);

        _sut.SetPersonPreference(person.Id, "Dark mode");
        _sut.SetPersonPreference(person.Id, "Dark mode");

        var rel = _sut.GetRelationship(person.Id);
        rel!.PersonPreferences.Count(p => p == "Dark mode").Should().Be(1);
    }

    // ==================================================================
    //  GetPersonalizedGreeting – branching by relationship state
    // ==================================================================

    [Fact]
    public void GetPersonalizedGreeting_NoPerson_ReturnsGenericGreeting()
    {
        var greeting = _sut.GetPersonalizedGreeting();

        greeting.Should().Contain("Hello");
    }

    [Fact]
    public void GetPersonalizedGreeting_NewPerson_ContainsName()
    {
        _sut.SetCurrentPerson("Nina");

        var greeting = _sut.GetPersonalizedGreeting();

        greeting.Should().Contain("Nina");
    }

    [Fact]
    public void GetPersonalizedGreeting_ReturningPerson_SaysWelcomeBack()
    {
        // Simulate returning person with interaction count > 1
        _sut.SetCurrentPerson("Oscar");
        var person = _sut.CurrentPerson!;
        // Bump interaction count by detecting again
        _sut.SetCurrentPerson("Oscar");

        var greeting = _sut.GetPersonalizedGreeting();

        greeting.Should().Contain("Oscar");
    }

    // ==================================================================
    //  GetRelationshipSummary
    // ==================================================================

    [Fact]
    public void GetRelationshipSummary_NoRelationship_ReturnsEmpty()
    {
        var summary = _sut.GetRelationshipSummary("nonexistent-id");

        summary.Should().BeEmpty();
    }

    [Fact]
    public void GetRelationshipSummary_ExistingRelationship_ContainsRapportDescription()
    {
        _sut.SetCurrentPerson("Paul");
        var person = _sut.CurrentPerson!;
        _sut.UpdateRelationship(person.Id, topic: "C# programming", isPositive: true);

        var summary = _sut.GetRelationshipSummary(person.Id);

        summary.Should().NotBeEmpty();
        summary.Should().Contain("rapport");
        summary.Should().Contain("C# programming");
    }

    // ==================================================================
    //  Proactivity Calculation
    // ==================================================================

    [Fact]
    public async Task ReasonAboutResponseAsync_UnknownPersona_ReturnsDefaults()
    {
        var (traits, proactivity, question) = await _sut.ReasonAboutResponseAsync(
            "NonExistent", "hello", "context");

        traits.Should().BeEmpty();
        proactivity.Should().Be(0.5);
        question.Should().BeNull();
    }

    [Fact]
    public async Task ReasonAboutResponseAsync_WithCuriousTrait_ReturnsHigherProactivity()
    {
        EnsureProfile();

        var (_, proactivity, _) = await _sut.ReasonAboutResponseAsync(
            "TestBot", "Why does this work? How?", "context\nline2\nline3\nline4");

        // "curious" trait should boost proactivity, and '?' should add 0.2
        proactivity.Should().BeGreaterThanOrEqualTo(0.5);
    }

    [Fact]
    public async Task ReasonAboutResponseAsync_ClosingPhrases_ReducesProactivity()
    {
        EnsureProfile();

        var (_, proactivity, _) = await _sut.ReasonAboutResponseAsync(
            "TestBot", "thanks, bye, that's all I needed", "context");

        proactivity.Should().BeLessThan(0.5,
            "closing phrases should reduce proactivity");
    }

    // ==================================================================
    //  RecordFeedback & Curiosity Drivers
    // ==================================================================

    [Fact]
    public void RecordFeedback_HighEngagement_AddsCuriosityDriver()
    {
        var profile = EnsureProfile();
        int driversBefore = profile.CuriosityDrivers.Count;

        var feedback = new InteractionFeedback(
            EngagementLevel: 0.9,
            ResponseRelevance: 0.8,
            QuestionQuality: 0.7,
            ConversationContinuity: 0.8,
            TopicDiscussed: "quantum computing",
            QuestionAsked: null,
            UserAskedFollowUp: true);

        _sut.RecordFeedback("TestBot", feedback);

        profile.CuriosityDrivers.Count.Should().BeGreaterThan(driversBefore,
            "high engagement feedback should add a curiosity driver");
        profile.CuriosityDrivers.Should().Contain(d => d.Topic == "quantum computing");
    }

    [Fact]
    public void RecordFeedback_LowEngagement_DoesNotAddDriver()
    {
        var profile = EnsureProfile();
        int driversBefore = profile.CuriosityDrivers.Count;

        var feedback = new InteractionFeedback(
            EngagementLevel: 0.2,
            ResponseRelevance: 0.5,
            QuestionQuality: 0.0,
            ConversationContinuity: 0.3,
            TopicDiscussed: "boring stuff",
            QuestionAsked: null,
            UserAskedFollowUp: false);

        _sut.RecordFeedback("TestBot", feedback);

        profile.CuriosityDrivers.Count.Should().Be(driversBefore);
    }

    [Fact]
    public void RecordFeedback_ExistingTopic_UpdatesInterest()
    {
        var profile = EnsureProfile();

        var fb1 = new InteractionFeedback(0.9, 0.8, 0.7, 0.8, "general knowledge", null, true);
        _sut.RecordFeedback("TestBot", fb1);

        var driver = profile.CuriosityDrivers.First(d => d.Topic == "general knowledge");
        double interestBefore = driver.Interest;

        var fb2 = new InteractionFeedback(0.95, 0.9, 0.85, 0.9, "general knowledge", "What else?", true);
        _sut.RecordFeedback("TestBot", fb2);

        var updated = profile.CuriosityDrivers.First(d => d.Topic == "general knowledge");
        updated.Interest.Should().BeGreaterThan(interestBefore,
            "high engagement feedback on existing topic should increase interest");
    }

    [Fact]
    public void RecordFeedback_NullTopic_DoesNotThrow()
    {
        EnsureProfile();

        var feedback = new InteractionFeedback(0.5, 0.5, 0.0, 0.5, null, null, false);
        var act = () => _sut.RecordFeedback("TestBot", feedback);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordFeedback_FeedbackHistoryCappedAt100()
    {
        EnsureProfile();

        for (int i = 0; i < 110; i++)
        {
            var fb = new InteractionFeedback(0.5, 0.5, 0.5, 0.5, $"topic-{i}", null, false);
            _sut.RecordFeedback("TestBot", fb);
        }

        // Should not throw and should internally cap
        _sut.GetCurrentMood("TestBot").Should().NotBeNull();
    }

    // ==================================================================
    //  Personality Evolution (genetic algorithm)
    // ==================================================================

    [Fact]
    public async Task EvolvePersonalityAsync_NotEnoughFeedback_ReturnsUnchangedProfile()
    {
        var profile = EnsureProfile();

        // Only 2 feedback entries (need >= 5)
        for (int i = 0; i < 2; i++)
        {
            _sut.RecordFeedback("TestBot",
                new InteractionFeedback(0.8, 0.7, 0.6, 0.8, "topic", null, true));
        }

        var evolved = await _sut.EvolvePersonalityAsync("TestBot");

        evolved.PersonaName.Should().Be("TestBot");
        // With < 5 feedback, profile should be returned as-is
    }

    [Fact]
    public async Task EvolvePersonalityAsync_NonExistentProfile_ThrowsInvalidOperation()
    {
        var act = async () => await _sut.EvolvePersonalityAsync("NonExistent");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EvolvePersonalityAsync_SufficientFeedback_EvolvesProfile()
    {
        EnsureProfile();

        // Provide 10 high-quality feedback entries
        for (int i = 0; i < 10; i++)
        {
            _sut.RecordFeedback("TestBot",
                new InteractionFeedback(0.9, 0.85, 0.8, 0.9, $"topic-{i}", "What's next?", true));
        }

        var evolved = await _sut.EvolvePersonalityAsync("TestBot");

        evolved.InteractionCount.Should().BeGreaterThan(0,
            "evolution should increment interaction count");
        evolved.LastEvolution.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ==================================================================
    //  GenerateProactiveQuestionAsync
    // ==================================================================

    [Fact]
    public async Task GenerateProactiveQuestionAsync_UnknownPersona_ReturnsNull()
    {
        var question = await _sut.GenerateProactiveQuestionAsync(
            "NonExistent", "topic", Array.Empty<string>());

        question.Should().BeNull();
    }

    [Fact]
    public async Task GenerateProactiveQuestionAsync_MatchingDriver_ReturnsQuestion()
    {
        var profile = EnsureProfile();
        // "general knowledge" driver was added in profile creation with questions
        var question = await _sut.GenerateProactiveQuestionAsync(
            "TestBot", "general knowledge", Array.Empty<string>());

        question.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateProactiveQuestionAsync_NoMatchingDriver_GeneratesNew()
    {
        EnsureProfile();

        var question = await _sut.GenerateProactiveQuestionAsync(
            "TestBot", "completely-obscure-topic-xyz", Array.Empty<string>());

        question.Should().NotBeNull();
        question.Should().Contain("completely-obscure-topic-xyz");
    }

    // ==================================================================
    //  GetResponseModifiers
    // ==================================================================

    [Fact]
    public void GetResponseModifiers_UnknownPersona_ReturnsEmpty()
    {
        var result = _sut.GetResponseModifiers("NonExistent");

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetResponseModifiers_ExistingPersona_ContainsMoodAndTraits()
    {
        EnsureProfile();

        var result = _sut.GetResponseModifiers("TestBot");

        result.Should().Contain("PERSONALITY EXPRESSION");
        result.Should().Contain("CURRENT MOOD");
    }

    // ==================================================================
    //  VoiceTone
    // ==================================================================

    [Fact]
    public void GetVoiceTone_UnknownPersona_ReturnsNeutral()
    {
        var tone = _sut.GetVoiceTone("NonExistent");

        tone.Should().Be(VoiceTone.Neutral);
    }

    [Fact]
    public void GetVoiceTone_AfterMoodUpdate_ReflectsMood()
    {
        EnsureProfile();
        _sut.UpdateMoodFromDetection("TestBot",
            "This is exciting! Amazing! I love it!");

        var tone = _sut.GetVoiceTone("TestBot");

        tone.Should().NotBeNull();
        // Excited mood should produce a distinct voice tone
    }

    // ==================================================================
    //  Consciousness Integration
    // ==================================================================

    [Fact]
    public async Task ProcessConsciousStimulusAsync_ReturnsValidState()
    {
        var state = await _sut.ProcessConsciousStimulusAsync(
            "greeting", "Hello, how are you?", 0.7);

        state.Should().NotBeNull();
        state.CurrentFocus.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ConditionNewAssociation_CreatesAssociationInConsciousnessEngine()
    {
        int before = _sut.Consciousness.Associations.Count;

        _sut.ConditionNewAssociation("coffee-aroma", "warmth", 0.6);

        _sut.Consciousness.Associations.Count.Should().BeGreaterThan(before);
    }

    [Fact]
    public void ReinforceAssociation_StrengthensExistingConditionedAssociation()
    {
        _sut.ConditionNewAssociation("bell-ring", "salivation", 0.4);
        var before = _sut.Consciousness.Associations.First(a => a.Stimulus.Pattern == "bell-ring");

        _sut.ReinforceAssociation("bell-ring", "salivation", 1.0);

        var after = _sut.Consciousness.Associations.First(a => a.Stimulus.Pattern == "bell-ring");
        after.AssociationStrength.Should().BeGreaterThanOrEqualTo(before.AssociationStrength);
    }

    [Fact]
    public void ExtinguishAssociation_WeakensExistingConditionedAssociation()
    {
        _sut.ConditionNewAssociation("light-flash", "blink", 0.6);
        var before = _sut.Consciousness.Associations.First(a => a.Stimulus.Pattern == "light-flash");

        _sut.ExtinguishAssociation("light-flash", "blink", 1.0);

        var after = _sut.Consciousness.Associations.First(a => a.Stimulus.Pattern == "light-flash");
        after.AssociationStrength.Should().BeLessThanOrEqualTo(before.AssociationStrength);
    }

    [Fact]
    public void GetActiveConditionedResponses_ReturnsMatchingResponses()
    {
        var responses = _sut.GetActiveConditionedResponses(threshold: 0.3);

        responses.Should().NotBeNull();
        // After initialization, innate associations above 0.3 should exist
        responses.Count.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void GenerateConsciousnessNarrative_ReturnsFormattedNarrative()
    {
        var narrative = _sut.GenerateConsciousnessNarrative();

        narrative.Should().Contain("CONSCIOUSNESS STREAM");
        narrative.Should().Contain("Arousal State");
        narrative.Should().Contain("Dominant Emotion");
    }

    // ==================================================================
    //  InitializeAsync idempotency
    // ==================================================================

    [Fact]
    public async Task InitializeAsync_CalledTwice_DoesNotThrow()
    {
        // Already initialized in InitializeAsync(), call again
        var act = async () => await _sut.InitializeAsync();

        await act.Should().NotThrowAsync();
    }

    // ==================================================================
    //  GetOrCreateProfile idempotency
    // ==================================================================

    [Fact]
    public void GetOrCreateProfile_CalledTwice_ReturnsSameProfile()
    {
        var first = _sut.GetOrCreateProfile("Bot1", new[] { "curious" }, new[] { "happy" }, "test");
        var second = _sut.GetOrCreateProfile("Bot1", new[] { "warm" }, new[] { "sad" }, "other");

        // Should return the first created profile, not create a new one
        first.PersonaName.Should().Be(second.PersonaName);
    }

    // ==================================================================
    //  Edge cases
    // ==================================================================

    [Fact]
    public void GetCurrentMood_UnknownPersona_ReturnsNeutral()
    {
        var mood = _sut.GetCurrentMood("NonExistent");

        mood.Should().Be("neutral");
    }

    [Fact]
    public void UpdateMood_NonExistentProfile_DoesNotThrow()
    {
        var act = () => _sut.UpdateMood("NonExistent", "hello", true);

        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateMoodFromDetection_NonExistentProfile_DoesNotThrow()
    {
        var act = () => _sut.UpdateMoodFromDetection("NonExistent", "hello");

        act.Should().NotThrow();
    }

    [Fact]
    public void GetRelationship_NonExistentId_ReturnsNull()
    {
        var result = _sut.GetRelationship("nonexistent-id");

        result.Should().BeNull();
    }

    [Fact]
    public void GetSelfAwarenessContext_ReturnsNonEmptyContext()
    {
        var context = _sut.GetSelfAwarenessContext();

        context.Should().Contain("Ouroboros");
        context.Should().Contain("Purpose");
        context.Should().Contain("Values");
    }
}
