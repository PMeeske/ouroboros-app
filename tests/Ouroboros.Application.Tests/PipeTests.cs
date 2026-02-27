// <copyright file="PipeTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.Interop;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class PipeTests
{
    // ======================================================================
    // Pipe<T, TR> struct — Value, operators, implicit conversion
    // ======================================================================

    [Fact]
    public void PipeStruct_Value_ShouldReturnInitialValue()
    {
        // Arrange & Act
        var pipe = new Pipe<int, string>(42);

        // Assert
        pipe.Value.Should().Be(42);
    }

    [Fact]
    public void PipeStruct_PipeOperator_WithPureFunction_ShouldTransform()
    {
        // Arrange
        var pipe = new Pipe<int, string>(42);
        Func<int, string> transform = x => $"value:{x}";

        // Act
        Pipe<string, string> result = pipe | transform;

        // Assert
        result.Value.Should().Be("value:42");
    }

    [Fact]
    public async Task PipeStruct_PipeOperator_WithAsyncStep_ShouldTransform()
    {
        // Arrange
        var pipe = new Pipe<int, string>(10);
        Step<int, string> step = x => Task.FromResult($"async:{x}");

        // Act
        var result = await (pipe | step);

        // Assert
        result.Should().Be("async:10");
    }

    [Fact]
    public void PipeStruct_ImplicitConversion_ShouldReturnValue()
    {
        // Arrange
        var pipe = new Pipe<int, string>(99);

        // Act
        int value = pipe;

        // Assert
        value.Should().Be(99);
    }

    [Fact]
    public void PipeStruct_Start_ShouldCreatePipeWithValue()
    {
        // Arrange & Act
        var pipe = Pipe.Start<string, int>("hello");

        // Assert
        pipe.Value.Should().Be("hello");
    }

    [Fact]
    public void PipeStruct_ChainedPureTransforms_ShouldCompose()
    {
        // Arrange
        var pipe = new Pipe<int, int>(5);
        Func<int, int> doubleIt = x => x * 2;
        Func<int, int> addOne = x => x + 1;

        // Act — chain two pure transforms
        Pipe<int, int> result = (pipe | doubleIt) | addOne;

        // Assert
        result.Value.Should().Be(11); // (5 * 2) + 1
    }

    [Fact]
    public void PipeStruct_DefaultValue_ShouldBeTypeDefault()
    {
        // Arrange & Act
        var pipe = new Pipe<int, int>(0);

        // Assert
        pipe.Value.Should().Be(0);
    }

    [Fact]
    public void PipeStruct_StringValue_ShouldWork()
    {
        // Arrange & Act
        var pipe = new Pipe<string, string>("hello");
        Func<string, string> upper = s => s.ToUpperInvariant();

        // Act
        Pipe<string, string> result = pipe | upper;

        // Assert
        result.Value.Should().Be("HELLO");
    }
}
