// <copyright file="PipelineDslTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class PipelineDslTests
{
    // ======================================================================
    // Tokenize
    // ======================================================================

    [Fact]
    public void Tokenize_Null_ShouldReturnEmpty()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize(null!);

        // Assert
        tokens.Should().BeEmpty();
    }

    [Fact]
    public void Tokenize_Empty_ShouldReturnEmpty()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize(string.Empty);

        // Assert
        tokens.Should().BeEmpty();
    }

    [Fact]
    public void Tokenize_Whitespace_ShouldReturnEmpty()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize("   ");

        // Assert
        tokens.Should().BeEmpty();
    }

    [Fact]
    public void Tokenize_SingleToken_ShouldReturnOneElement()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize("Set");

        // Assert
        tokens.Should().HaveCount(1);
        tokens[0].Should().Be("Set");
    }

    [Fact]
    public void Tokenize_TwoTokensSeparatedByPipe_ShouldSplit()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize("Set | Draft");

        // Assert
        tokens.Should().HaveCount(2);
        tokens[0].Should().Be("Set");
        tokens[1].Should().Be("Draft");
    }

    [Fact]
    public void Tokenize_MultipleTokens_ShouldSplitAll()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize("Set | Draft | Critique | Improve");

        // Assert
        tokens.Should().HaveCount(4);
        tokens[0].Should().Be("Set");
        tokens[1].Should().Be("Draft");
        tokens[2].Should().Be("Critique");
        tokens[3].Should().Be("Improve");
    }

    [Fact]
    public void Tokenize_WithParentheses_ShouldPreservePipeInParens()
    {
        // Arrange: a pipe inside parentheses should NOT be treated as a separator
        // Act
        var tokens = PipelineDsl.Tokenize("Set('a|b') | Draft");

        // Assert
        tokens.Should().HaveCount(2);
        tokens[0].Should().Be("Set('a|b')");
        tokens[1].Should().Be("Draft");
    }

    [Fact]
    public void Tokenize_NestedParentheses_ShouldPreserve()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize("Set(func(x|y)) | Draft");

        // Assert
        tokens.Should().HaveCount(2);
        tokens[0].Should().Be("Set(func(x|y))");
        tokens[1].Should().Be("Draft");
    }

    [Fact]
    public void Tokenize_SingleQuotedPipe_ShouldNotSplit()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize("Set('hello | world')");

        // Assert
        tokens.Should().HaveCount(1);
        tokens[0].Should().Contain("|");
    }

    [Fact]
    public void Tokenize_DoubleQuotedPipe_ShouldNotSplit()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize("Set(\"hello | world\")");

        // Assert
        tokens.Should().HaveCount(1);
        tokens[0].Should().Contain("|");
    }

    [Fact]
    public void Tokenize_EmptyTokenBetweenPipes_ShouldBeSkipped()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize("Set || Draft");

        // Assert — the empty segment between || is trimmed and empty, so skipped
        tokens.Should().HaveCount(2);
        tokens[0].Should().Be("Set");
        tokens[1].Should().Be("Draft");
    }

    [Fact]
    public void Tokenize_LeadingPipe_ShouldSkipEmptyLeadingToken()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize("| Set");

        // Assert
        tokens.Should().HaveCount(1);
        tokens[0].Should().Be("Set");
    }

    [Fact]
    public void Tokenize_TrailingPipe_ShouldSkipEmptyTrailingToken()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize("Set |");

        // Assert
        tokens.Should().HaveCount(1);
        tokens[0].Should().Be("Set");
    }

    [Fact]
    public void Tokenize_TokenWithArguments_ShouldPreserveArgs()
    {
        // Arrange & Act
        var tokens = PipelineDsl.Tokenize("Set('hello') | Draft | Improve(3)");

        // Assert
        tokens.Should().HaveCount(3);
        tokens[0].Should().Be("Set('hello')");
        tokens[1].Should().Be("Draft");
        tokens[2].Should().Be("Improve(3)");
    }

    // ======================================================================
    // Explain
    // ======================================================================

    [Fact]
    public void Explain_EmptyDsl_ShouldContainHeader()
    {
        // Arrange & Act
        string explanation = PipelineDsl.Explain(string.Empty);

        // Assert
        explanation.Should().Contain("Pipeline tokens:");
        explanation.Should().Contain("Available token groups:");
    }

    [Fact]
    public void Explain_KnownToken_ShouldShowMethod()
    {
        // Arrange & Act
        string explanation = PipelineDsl.Explain("Set");

        // Assert
        explanation.Should().Contain("Set");
        explanation.Should().Contain("SetPrompt");
    }

    [Fact]
    public void Explain_UnknownToken_ShouldShowNoOp()
    {
        // Arrange & Act
        string explanation = PipelineDsl.Explain("__unknown_xyz__");

        // Assert
        explanation.Should().Contain("__unknown_xyz__");
        explanation.Should().Contain("(no-op)");
    }

    [Fact]
    public void Explain_TokenWithArgs_ShouldShowArgs()
    {
        // Arrange & Act
        string explanation = PipelineDsl.Explain("Set(hello)");

        // Assert
        explanation.Should().Contain("Set");
        explanation.Should().Contain("hello");
    }

    // ======================================================================
    // Build — verifying it produces a callable step (without full pipeline state)
    // ======================================================================

    [Fact]
    public void Build_EmptyDsl_ShouldReturnIdentityStep()
    {
        // Arrange & Act — empty DSL yields an identity step (no tokens to compose)
        var step = PipelineDsl.Build(string.Empty);

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public void Build_UnknownToken_ShouldNotThrow()
    {
        // Arrange & Act — unknown tokens become no-ops
        var act = () => PipelineDsl.Build("__nonexistent__");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Build_KnownToken_ShouldReturnStep()
    {
        // Arrange & Act
        var step = PipelineDsl.Build("TraceOn");

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public void Build_MultipleTokens_ShouldReturnStep()
    {
        // Arrange & Act
        var step = PipelineDsl.Build("TraceOn | TraceOff");

        // Assert
        step.Should().NotBeNull();
    }

    // ======================================================================
    // ParseToken (tested indirectly via Build/Explain)
    // ======================================================================

    [Fact]
    public void Build_StepGenericSyntax_ShouldNormalizeToSet()
    {
        // Arrange — "Step<string,string>(value)" should be normalized to "Set(value)"
        // Act — Build should not throw, and the token should resolve
        var act = () => PipelineDsl.Build("Step<string,string>('hello')");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Explain_StepGenericSyntax_ShouldResolveToSet()
    {
        // Arrange & Act
        string explanation = PipelineDsl.Explain("Step<string,string>('hello')");

        // Assert — should resolve to Set (which maps to SetPrompt)
        explanation.Should().Contain("Set");
    }
}
