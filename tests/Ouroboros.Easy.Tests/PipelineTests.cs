using EasyPipeline = Ouroboros.Easy.Pipeline;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class PipelineTests
{
    [Fact]
    public void Create_ReturnsNewInstance()
    {
        // Act
        var pipeline = EasyPipeline.Create();

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void About_NullTopic_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = EasyPipeline.Create();

        // Act
        var act = () => pipeline.About(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("topic");
    }

    [Fact]
    public void WithModel_NullModelName_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = EasyPipeline.Create();

        // Act
        var act = () => pipeline.WithModel(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("modelName");
    }

    [Fact]
    public void WithOllamaEndpoint_NullEndpoint_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = EasyPipeline.Create();

        // Act
        var act = () => pipeline.WithOllamaEndpoint(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("endpoint");
    }

    [Fact]
    public void WithTools_NullTools_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = EasyPipeline.Create();

        // Act
        var act = () => pipeline.WithTools(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tools");
    }

    [Fact]
    public void WithEmbedding_NullEmbedding_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = EasyPipeline.Create();

        // Act
        var act = () => pipeline.WithEmbedding(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("embedding");
    }

    [Fact]
    public void WithTemperature_ClampsAboveOne_ReturnsOne()
    {
        // Arrange
        var pipeline = EasyPipeline.Create()
            .About("test")
            .WithModel("llama3")
            .Draft()
            .WithTemperature(2.0);

        // Act
        var dsl = pipeline.ToDSL();

        // Assert - temperature is clamped to 1.0
        dsl.Should().Contain("Temperature: 1");
    }

    [Fact]
    public void WithTemperature_ClampsBelowZero_ReturnsZero()
    {
        // Arrange
        var pipeline = EasyPipeline.Create()
            .About("test")
            .WithModel("llama3")
            .Draft()
            .WithTemperature(-0.5);

        // Act
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("Temperature: 0");
    }

    [Fact]
    public async Task RunAsync_NoTopic_ReturnsFailure()
    {
        // Arrange
        var pipeline = EasyPipeline.Create()
            .WithModel("llama3")
            .Draft();

        // Act
        var result = await pipeline.RunAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Topic must be set");
    }

    [Fact]
    public async Task RunAsync_NoModel_ReturnsFailure()
    {
        // Arrange
        var pipeline = EasyPipeline.Create()
            .About("test topic")
            .Draft();

        // Act
        var result = await pipeline.RunAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Model must be set");
    }

    [Fact]
    public async Task RunAsync_NoStages_ReturnsFailure()
    {
        // Arrange
        var pipeline = EasyPipeline.Create()
            .About("test topic")
            .WithModel("llama3");

        // Act
        var result = await pipeline.RunAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("At least one stage must be enabled");
    }

    [Fact]
    public void ToDSL_WithAllStages_ContainsAllStageNames()
    {
        // Arrange
        var pipeline = EasyPipeline.Create()
            .About("quantum computing")
            .WithModel("llama3")
            .Draft()
            .Critique()
            .Improve()
            .Summarize();

        // Act
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("draft");
        dsl.Should().Contain("critique");
        dsl.Should().Contain("improve");
        dsl.Should().Contain("summarize");
        dsl.Should().Contain("quantum computing");
        dsl.Should().Contain("llama3");
    }

    [Fact]
    public void ToDSL_NoStages_ContainsNone()
    {
        // Arrange
        var pipeline = EasyPipeline.Create()
            .About("test")
            .WithModel("llama3");

        // Act
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("Stages: none");
    }

    [Fact]
    public void ToDSL_WithCustomEndpoint_ShowsEndpoint()
    {
        // Arrange
        var pipeline = EasyPipeline.Create()
            .About("test")
            .WithModel("llama3")
            .Draft()
            .WithOllamaEndpoint("http://myhost:11434");

        // Act
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("http://myhost:11434");
    }

    [Fact]
    public void ToDSL_WithDefaultEndpoint_ShowsDefault()
    {
        // Arrange
        var pipeline = EasyPipeline.Create()
            .About("test")
            .WithModel("llama3")
            .Draft();

        // Act
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("default (localhost:11434)");
    }

    [Fact]
    public void FluentChaining_AllMethods_ReturnSameInstance()
    {
        // Arrange
        var pipeline = EasyPipeline.Create();

        // Act
        var result = pipeline
            .About("test")
            .Draft()
            .Critique()
            .Improve()
            .Summarize()
            .WithModel("llama3")
            .WithTemperature(0.5);

        // Assert
        result.Should().BeSameAs(pipeline);
    }
}
