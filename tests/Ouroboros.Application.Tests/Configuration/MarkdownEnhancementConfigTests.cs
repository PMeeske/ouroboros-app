// <copyright file="MarkdownEnhancementConfigTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.Configuration;
using Xunit;

namespace Ouroboros.Tests.Configuration;

[Trait("Category", "Unit")]
public class MarkdownEnhancementConfigTests
{
    [Fact]
    public void Constructor_WithRequiredFilePath_ShouldSetFilePath()
    {
        // Arrange & Act
        var config = new MarkdownEnhancementConfig { FilePath = "/tmp/readme.md" };

        // Assert
        config.FilePath.Should().Be("/tmp/readme.md");
    }

    [Fact]
    public void Iterations_Default_ShouldBeOne()
    {
        // Arrange & Act
        var config = new MarkdownEnhancementConfig { FilePath = "test.md" };

        // Assert
        config.Iterations.Should().Be(1);
    }

    [Fact]
    public void ContextCount_Default_ShouldBeEight()
    {
        // Arrange & Act
        var config = new MarkdownEnhancementConfig { FilePath = "test.md" };

        // Assert
        config.ContextCount.Should().Be(8);
    }

    [Fact]
    public void CreateBackup_Default_ShouldBeTrue()
    {
        // Arrange & Act
        var config = new MarkdownEnhancementConfig { FilePath = "test.md" };

        // Assert
        config.CreateBackup.Should().BeTrue();
    }

    [Fact]
    public void Goal_Default_ShouldBeNull()
    {
        // Arrange & Act
        var config = new MarkdownEnhancementConfig { FilePath = "test.md" };

        // Assert
        config.Goal.Should().BeNull();
    }

    [Fact]
    public void AllProperties_ShouldBeSettable()
    {
        // Arrange & Act
        var config = new MarkdownEnhancementConfig
        {
            FilePath = "/docs/readme.md",
            Iterations = 5,
            ContextCount = 16,
            CreateBackup = false,
            Goal = "Improve readability"
        };

        // Assert
        config.FilePath.Should().Be("/docs/readme.md");
        config.Iterations.Should().Be(5);
        config.ContextCount.Should().Be(16);
        config.CreateBackup.Should().BeFalse();
        config.Goal.Should().Be("Improve readability");
    }

    [Fact]
    public void RecordEquality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var a = new MarkdownEnhancementConfig { FilePath = "test.md", Iterations = 2 };
        var b = new MarkdownEnhancementConfig { FilePath = "test.md", Iterations = 2 };

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var a = new MarkdownEnhancementConfig { FilePath = "test.md", Iterations = 2 };
        var b = new MarkdownEnhancementConfig { FilePath = "test.md", Iterations = 3 };

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new MarkdownEnhancementConfig
        {
            FilePath = "test.md",
            Iterations = 1,
            Goal = "Original goal"
        };

        // Act
        var modified = original with { Iterations = 10, Goal = "New goal" };

        // Assert
        modified.FilePath.Should().Be("test.md");
        modified.Iterations.Should().Be(10);
        modified.Goal.Should().Be("New goal");
        original.Iterations.Should().Be(1, "original should not be changed");
    }
}
