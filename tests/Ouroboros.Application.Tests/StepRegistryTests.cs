// <copyright file="StepRegistryTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Reflection;
using FluentAssertions;
using Ouroboros.Application;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class StepRegistryTests
{
    // --- Tokens ---

    [Fact]
    public void Tokens_ShouldNotBeEmpty()
    {
        // Arrange & Act
        var tokens = StepRegistry.Tokens;

        // Assert
        tokens.Should().NotBeEmpty("the assembly contains at least one [PipelineToken] method");
    }

    [Fact]
    public void Tokens_ShouldContainKnownToken_Set()
    {
        // Arrange & Act
        var tokens = StepRegistry.Tokens;

        // Assert
        tokens.Should().Contain(
            t => t.Equals("Set", StringComparison.OrdinalIgnoreCase),
            "CliSteps.SetPrompt is annotated with [PipelineToken(\"Set\", ...)]");
    }

    [Fact]
    public void Tokens_ShouldBeCaseInsensitive()
    {
        // Arrange & Act — look up a known token in different casing
        bool resolvedLower = StepRegistry.TryResolveInfo("set", out _);
        bool resolvedUpper = StepRegistry.TryResolveInfo("SET", out _);
        bool resolvedMixed = StepRegistry.TryResolveInfo("Set", out _);

        // Assert
        resolvedLower.Should().BeTrue();
        resolvedUpper.Should().BeTrue();
        resolvedMixed.Should().BeTrue();
    }

    // --- TryResolve ---

    [Fact]
    public void TryResolve_KnownToken_ShouldReturnTrueAndStep()
    {
        // Arrange & Act
        bool found = StepRegistry.TryResolve("Set", null, out var step);

        // Assert
        found.Should().BeTrue();
        step.Should().NotBeNull();
    }

    [Fact]
    public void TryResolve_UnknownToken_ShouldReturnFalse()
    {
        // Arrange & Act
        bool found = StepRegistry.TryResolve("__nonexistent_token_xyz__", null, out var step);

        // Assert
        found.Should().BeFalse();
        step.Should().BeNull();
    }

    [Fact]
    public void TryResolve_WithArgs_ShouldReturnStep()
    {
        // Arrange & Act
        bool found = StepRegistry.TryResolve("Set", "'hello world'", out var step);

        // Assert
        found.Should().BeTrue();
        step.Should().NotBeNull();
    }

    // --- TryResolveInfo ---

    [Fact]
    public void TryResolveInfo_KnownToken_ShouldReturnMethodInfo()
    {
        // Arrange & Act
        bool found = StepRegistry.TryResolveInfo("Set", out MethodInfo? mi);

        // Assert
        found.Should().BeTrue();
        mi.Should().NotBeNull();
        mi!.Name.Should().Be("SetPrompt");
    }

    [Fact]
    public void TryResolveInfo_UnknownToken_ShouldReturnFalse()
    {
        // Arrange & Act
        bool found = StepRegistry.TryResolveInfo("__nonexistent__", out MethodInfo? mi);

        // Assert
        found.Should().BeFalse();
        mi.Should().BeNull();
    }

    // --- GetTokenGroups ---

    [Fact]
    public void GetTokenGroups_ShouldReturnNonEmpty()
    {
        // Arrange & Act
        var groups = StepRegistry.GetTokenGroups().ToList();

        // Assert
        groups.Should().NotBeEmpty();
    }

    [Fact]
    public void GetTokenGroups_ShouldGroupAliases()
    {
        // Arrange & Act — "Set" and "SetPrompt" and "Step<string,string>" should map to one method
        var groups = StepRegistry.GetTokenGroups().ToList();
        var setGroup = groups.FirstOrDefault(g => g.Names.Any(n => n.Equals("Set", StringComparison.OrdinalIgnoreCase)));

        // Assert
        setGroup.Names.Should().NotBeNull();
        setGroup.Names.Count.Should().BeGreaterThanOrEqualTo(2, "Set has at least two aliases");
    }

    [Fact]
    public void GetTokenGroups_NamesAreSorted()
    {
        // Arrange & Act
        var groups = StepRegistry.GetTokenGroups().ToList();

        // Assert
        foreach (var group in groups)
        {
            var sorted = group.Names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            group.Names.Should().BeEquivalentTo(sorted, options => options.WithStrictOrdering(),
                "names within each group should be sorted case-insensitively");
        }
    }

    // --- Thread Safety (the Lazy<> guarantees this, but validate no crash) ---

    [Fact]
    public void Tokens_CalledMultipleTimes_ReturnsSameContent()
    {
        // Arrange & Act
        var first = StepRegistry.Tokens;
        var second = StepRegistry.Tokens;

        // Assert
        first.Should().BeEquivalentTo(second);
    }
}
