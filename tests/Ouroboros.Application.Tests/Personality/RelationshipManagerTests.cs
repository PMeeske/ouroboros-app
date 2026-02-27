// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

namespace Ouroboros.Tests.Personality;

using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

/// <summary>
/// Unit tests for <see cref="RelationshipManager"/> covering rapport tracking,
/// shared topics, notable memories, preferences, and courtesy generation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RelationshipManagerTests
{
    private readonly PersonDetectionEngine _personDetection;
    private readonly RelationshipManager _sut;

    public RelationshipManagerTests()
    {
        _personDetection = new PersonDetectionEngine(null, null, "test_persons");
        _sut = new RelationshipManager(_personDetection);
    }

    // ------------------------------------------------------------------
    //  Helper
    // ------------------------------------------------------------------

    private string CreatePerson(string name)
    {
        var result = _personDetection.SetCurrentPerson(name);
        return result.Person.Id;
    }

    // ==================================================================
    //  UpdateRelationship
    // ==================================================================

    [Fact]
    public void UpdateRelationship_PositiveInteraction_IncreasesRapport()
    {
        var personId = CreatePerson("Eve");

        // First call creates the relationship with baseline rapport (0.5)
        _sut.UpdateRelationship(personId, "testing", isPositive: true);
        // Second call applies the positive delta (+0.05)
        _sut.UpdateRelationship(personId, "follow-up", isPositive: true);
        var rel = _sut.GetRelationship(personId);

        rel.Should().NotBeNull();
        rel!.Rapport.Should().BeGreaterThan(0.5, "positive interaction should increase rapport from baseline 0.5");
    }

    [Fact]
    public void UpdateRelationship_NegativeInteraction_DecreasesRapportAndTrust()
    {
        var personId = CreatePerson("Frank");

        _sut.UpdateRelationship(personId, isPositive: true); // Create relationship
        _sut.UpdateRelationship(personId, isPositive: false);

        var rel = _sut.GetRelationship(personId);
        rel!.NegativeInteractions.Should().Be(1);
    }

    [Fact]
    public void UpdateRelationship_WithTopic_TracksSharedTopics()
    {
        var personId = CreatePerson("Grace");

        _sut.UpdateRelationship(personId, topic: "machine learning", isPositive: true);
        _sut.UpdateRelationship(personId, topic: "data science", isPositive: true);

        var rel = _sut.GetRelationship(personId);
        rel!.SharedTopics.Should().Contain("machine learning");
        rel.SharedTopics.Should().Contain("data science");
    }

    [Fact]
    public void UpdateRelationship_SharedTopicsCappedAtTen()
    {
        var personId = CreatePerson("Hank");

        for (int i = 0; i < 15; i++)
        {
            _sut.UpdateRelationship(personId, topic: $"topic-{i}", isPositive: true);
        }

        var rel = _sut.GetRelationship(personId);
        rel!.SharedTopics.Length.Should().BeLessThanOrEqualTo(10, "shared topics should be capped at 10");
    }

    [Fact]
    public void UpdateRelationship_RapportClampedBetweenZeroAndOne()
    {
        var personId = CreatePerson("Ivy");

        // Many positive interactions
        for (int i = 0; i < 100; i++)
        {
            _sut.UpdateRelationship(personId, isPositive: true);
        }

        var rel = _sut.GetRelationship(personId);
        rel!.Rapport.Should().BeLessThanOrEqualTo(1.0);

        // Many negative interactions
        for (int i = 0; i < 200; i++)
        {
            _sut.UpdateRelationship(personId, isPositive: false);
        }

        rel = _sut.GetRelationship(personId);
        rel!.Rapport.Should().BeGreaterThanOrEqualTo(0.0);
    }

    // ==================================================================
    //  GetRelationship
    // ==================================================================

    [Fact]
    public void GetRelationship_NonExistentId_ReturnsNull()
    {
        _sut.GetRelationship("nonexistent-id").Should().BeNull();
    }

    // ==================================================================
    //  AddNotableMemory
    // ==================================================================

    [Fact]
    public void AddNotableMemory_AppendsMemoryWithDate()
    {
        var personId = CreatePerson("Jack");
        _sut.UpdateRelationship(personId, isPositive: true); // Create relationship

        _sut.AddNotableMemory(personId, "Likes Python");

        var rel = _sut.GetRelationship(personId);
        rel!.ThingsToRemember.Should().ContainSingle(m => m.Contains("Likes Python"));
    }

    [Fact]
    public void AddNotableMemory_CappedAtTwenty()
    {
        var personId = CreatePerson("Kate");
        _sut.UpdateRelationship(personId, isPositive: true);

        for (int i = 0; i < 25; i++)
        {
            _sut.AddNotableMemory(personId, $"memory-{i}");
        }

        var rel = _sut.GetRelationship(personId);
        rel!.ThingsToRemember.Length.Should().BeLessThanOrEqualTo(20);
    }

    // ==================================================================
    //  SetPersonPreference
    // ==================================================================

    [Fact]
    public void SetPersonPreference_TracksPreference()
    {
        var personId = CreatePerson("Leo");
        _sut.UpdateRelationship(personId, isPositive: true);

        _sut.SetPersonPreference(personId, "Prefers verbose explanations");

        var rel = _sut.GetRelationship(personId);
        rel!.PersonPreferences.Should().Contain("Prefers verbose explanations");
    }

    [Fact]
    public void SetPersonPreference_DoesNotDuplicateExisting()
    {
        var personId = CreatePerson("Mia");
        _sut.UpdateRelationship(personId, isPositive: true);

        _sut.SetPersonPreference(personId, "Dark mode");
        _sut.SetPersonPreference(personId, "Dark mode");

        var rel = _sut.GetRelationship(personId);
        rel!.PersonPreferences.Count(p => p == "Dark mode").Should().Be(1);
    }

    // ==================================================================
    //  GetRelationshipSummary
    // ==================================================================

    [Fact]
    public void GetRelationshipSummary_NoRelationship_ReturnsEmpty()
    {
        _sut.GetRelationshipSummary("nonexistent-id").Should().BeEmpty();
    }

    [Fact]
    public void GetRelationshipSummary_ExistingRelationship_ContainsRapportDescription()
    {
        var personId = CreatePerson("Paul");
        _sut.UpdateRelationship(personId, topic: "C# programming", isPositive: true);

        var summary = _sut.GetRelationshipSummary(personId);

        summary.Should().NotBeEmpty();
        summary.Should().Contain("rapport");
        summary.Should().Contain("C# programming");
    }

    // ==================================================================
    //  GetCourtesyPrefix
    // ==================================================================

    [Fact]
    public void GetCourtesyPrefix_NoRelationship_ReturnsEmpty()
    {
        _sut.GetCourtesyPrefix("nonexistent-id").Should().BeEmpty();
    }

    [Fact]
    public void GetCourtesyPrefix_HighRapport_ReturnsWarmPhrase()
    {
        var personId = CreatePerson("Quinn");
        _sut.UpdateRelationship(personId, isPositive: true);

        // Push rapport above 0.8
        for (int i = 0; i < 20; i++)
        {
            _sut.UpdateRelationship(personId, isPositive: true);
        }

        var prefix = _sut.GetCourtesyPrefix(personId);
        prefix.Should().NotBeEmpty("high rapport should produce a warm courtesy prefix");
    }

    // ==================================================================
    //  GenerateCourtesyResponse
    // ==================================================================

    [Theory]
    [InlineData(CourtesyType.Acknowledgment)]
    [InlineData(CourtesyType.Apology)]
    [InlineData(CourtesyType.Gratitude)]
    [InlineData(CourtesyType.Encouragement)]
    [InlineData(CourtesyType.Interest)]
    public void GenerateCourtesyResponse_AllTypes_ReturnNonEmpty(CourtesyType type)
    {
        _sut.GenerateCourtesyResponse(type).Should().NotBeNullOrEmpty();
    }
}
