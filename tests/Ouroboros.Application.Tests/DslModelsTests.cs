using FluentAssertions;
using Ouroboros.Application;
using Xunit;
using DslSuggestion = Ouroboros.Application.DslSuggestion;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class DslModelsTests
{
    [Fact]
    public void DslSuggestion_Defaults_ShouldHaveExpectedValues()
    {
        var suggestion = new DslSuggestion();

        suggestion.Token.Should().BeEmpty();
        suggestion.Explanation.Should().BeEmpty();
        suggestion.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void DslSuggestion_SetProperties_ShouldRetainValues()
    {
        var suggestion = new DslSuggestion
        {
            Token = "UseQdrant",
            Explanation = "Use Qdrant vector store",
            Confidence = 0.9
        };

        suggestion.Token.Should().Be("UseQdrant");
        suggestion.Explanation.Should().Be("Use Qdrant vector store");
        suggestion.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void DslValidationResult_Defaults_ShouldBeInvalid()
    {
        var result = new DslValidationResult();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
        result.Suggestions.Should().BeEmpty();
        result.FixedDsl.Should().BeNull();
    }

    [Fact]
    public void DslValidationResult_Valid_ShouldWork()
    {
        var result = new DslValidationResult
        {
            IsValid = true,
            Errors = new List<string>(),
            Warnings = new List<string> { "Consider adding error handling" },
            Suggestions = new List<string> { "Use UseQdrant" },
            FixedDsl = "Topic: test\nDraft"
        };

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().HaveCount(1);
        result.Suggestions.Should().HaveCount(1);
        result.FixedDsl.Should().NotBeNull();
    }
}
