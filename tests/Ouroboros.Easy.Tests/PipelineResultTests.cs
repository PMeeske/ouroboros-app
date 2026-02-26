using EasyPipeline = Ouroboros.Easy.Pipeline;
using Ouroboros.Easy;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class PipelineResultTests
{
    [Fact]
    public async Task Failure_ViaRunAsync_HasCorrectProperties()
    {
        // We test through a pipeline that fails validation
        var pipeline = EasyPipeline.Create()
            .WithModel("llama3")
            .Draft();

        // Act - no topic set, so it fails
        var result = await pipeline.RunAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Output.Should().BeNull();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOutputOrThrow_OnFailure_ThrowsInvalidOperationException()
    {
        // Arrange - create a failure result via pipeline
        var pipeline = EasyPipeline.Create().WithModel("test").Draft();
        var result = await pipeline.RunAsync();

        // Act
        var act = () => result.GetOutputOrThrow();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pipeline execution failed*");
    }

    [Fact]
    public async Task FailureResult_Error_ContainsMessage()
    {
        // Arrange
        var pipeline = EasyPipeline.Create()
            .About("test")
            .WithModel("llama3");
        // No stages enabled

        // Act
        var result = await pipeline.RunAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("At least one stage must be enabled");
    }
}
