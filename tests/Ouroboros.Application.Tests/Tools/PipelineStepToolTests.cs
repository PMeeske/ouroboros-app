// <copyright file="PipelineStepToolTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application;
using Ouroboros.Application.Tools;
using Xunit;

namespace Ouroboros.Tests.Tools;

[Trait("Category", "Unit")]
public class PipelineStepToolTests
{
    // ======================================================================
    // Constructor
    // ======================================================================

    [Fact]
    public void Constructor_NullStepName_ShouldThrow()
    {
        // Arrange & Act
        var act = () => new PipelineStepTool(null!, "desc", _ => s => Task.FromResult(s));

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("stepName");
    }

    [Fact]
    public void Constructor_NullStepFactory_ShouldThrow()
    {
        // Arrange & Act
        var act = () => new PipelineStepTool("test", "desc", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("stepFactory");
    }

    [Fact]
    public void Constructor_ValidArgs_ShouldSetProperties()
    {
        // Arrange & Act
        var tool = new PipelineStepTool("Draft", "Generate draft", _ => s => Task.FromResult(s));

        // Assert
        tool.Name.Should().Be("run_draft");
        tool.Description.Should().Be("Generate draft");
        tool.JsonSchema.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Name_ShouldBeLowercase_WithRunPrefix()
    {
        // Arrange & Act
        var tool = new PipelineStepTool("SetPrompt", "Set prompt", _ => s => Task.FromResult(s));

        // Assert
        tool.Name.Should().Be("run_setprompt");
    }

    [Fact]
    public void JsonSchema_ShouldContainArgsProperty()
    {
        // Arrange & Act
        var tool = new PipelineStepTool("test", "desc", _ => s => Task.FromResult(s));

        // Assert
        tool.JsonSchema.Should().Contain("\"args\"");
        tool.JsonSchema.Should().Contain("\"type\"");
    }

    // ======================================================================
    // InvokeAsync — no pipeline state
    // ======================================================================

    [Fact]
    public async Task InvokeAsync_WithoutPipelineState_ShouldReturnFailure()
    {
        // Arrange
        var tool = new PipelineStepTool("test", "desc", _ => s => Task.FromResult(s));

        // Act
        var result = await tool.InvokeAsync("input");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Pipeline state not initialized");
    }

    // ======================================================================
    // FromStepName
    // ======================================================================

    [Fact]
    public void FromStepName_ShouldReturnTool()
    {
        // Arrange & Act
        var tool = PipelineStepTool.FromStepName("Set", "Set the prompt");

        // Assert
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("run_set");
        tool.Description.Should().Be("Set the prompt");
    }

    [Fact]
    public void FromStepName_UnknownStep_ShouldStillCreateTool()
    {
        // Arrange & Act — unknown steps create tools that resolve to no-ops at invocation
        var tool = PipelineStepTool.FromStepName("__nonexistent__", "No-op step");

        // Assert
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("run___nonexistent__");
    }
}
