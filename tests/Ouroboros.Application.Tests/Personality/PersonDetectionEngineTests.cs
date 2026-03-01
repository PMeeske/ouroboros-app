// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

namespace Ouroboros.Tests.Personality;

using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

/// <summary>
/// Unit tests for <see cref="PersonDetectionEngine"/> covering name extraction,
/// person creation, returning-person recognition, and communication style analysis.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PersonDetectionEngineTests
{
    private readonly PersonDetectionEngine _sut;

    public PersonDetectionEngineTests()
    {
        // No Qdrant in unit tests
        _sut = new PersonDetectionEngine(null, null, "test_persons");
    }

    // ==================================================================
    //  SetCurrentPerson
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
    public void SetCurrentPerson_DifferentNames_CreatesDifferentPersons()
    {
        _sut.SetCurrentPerson("Carol");
        _sut.SetCurrentPerson("Dave");

        _sut.KnownPersons.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    // ==================================================================
    //  DetectPersonAsync
    // ==================================================================

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
        await _sut.DetectPersonAsync("My name is Diana");
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
    //  ExtractNameFromMessage (static)
    // ==================================================================

    [Theory]
    [InlineData("My name is Alice", "Alice")]
    [InlineData("I'm Bob", "Bob")]
    [InlineData("Call me Charlie", "Charlie")]
    [InlineData("Ich bin Markus", "Markus")]
    [InlineData("Je m'appelle Marie", "Marie")]
    [InlineData("Me llamo Carlos", "Carlos")]
    public void ExtractNameFromMessage_RecognizesMultilingualNames(string message, string expectedName)
    {
        var (name, confidence) = PersonDetectionEngine.ExtractNameFromMessage(message);

        name.Should().Be(expectedName);
        confidence.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void ExtractNameFromMessage_NoName_ReturnsNull()
    {
        var (name, _) = PersonDetectionEngine.ExtractNameFromMessage("Can you help me with something?");
        name.Should().BeNull();
    }

    // ==================================================================
    //  AnalyzeCommunicationStyle (static)
    // ==================================================================

    [Fact]
    public void AnalyzeCommunicationStyle_VerboseInput_HasHighVerbosity()
    {
        var message = string.Join(" ", Enumerable.Range(0, 80).Select(i => $"word{i}"));
        var style = PersonDetectionEngine.AnalyzeCommunicationStyle(message, Array.Empty<string>());

        style.Verbosity.Should().BeGreaterThan(0.5, "80 words should be verbose");
    }

    [Fact]
    public void AnalyzeCommunicationStyle_QuestionInput_HasHighQuestionFrequency()
    {
        var style = PersonDetectionEngine.AnalyzeCommunicationStyle("What is this?", Array.Empty<string>());

        style.QuestionFrequency.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void AnalyzeCommunicationStyle_DefaultValues_AllInRange()
    {
        var style = PersonDetectionEngine.AnalyzeCommunicationStyle("Hello world", Array.Empty<string>());

        style.Verbosity.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
        style.QuestionFrequency.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
        style.EmoticonUsage.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
        style.PunctuationStyle.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
    }

    // ==================================================================
    //  GetPerson
    // ==================================================================

    [Fact]
    public void GetPerson_ExistingPerson_ReturnsPerson()
    {
        var result = _sut.SetCurrentPerson("Eve");
        var person = _sut.GetPerson(result.Person.Id);

        person.Should().NotBeNull();
        person!.Name.Should().Be("Eve");
    }

    [Fact]
    public void GetPerson_NonExistentId_ReturnsNull()
    {
        var person = _sut.GetPerson("nonexistent-id");
        person.Should().BeNull();
    }

    // ==================================================================
    //  KnownPersons collection
    // ==================================================================

    [Fact]
    public void KnownPersons_AfterMultipleDetections_ContainsAll()
    {
        _sut.SetCurrentPerson("Alice");
        _sut.SetCurrentPerson("Bob");
        _sut.SetCurrentPerson("Charlie");

        _sut.KnownPersons.Should().HaveCountGreaterThanOrEqualTo(3);
    }
}
