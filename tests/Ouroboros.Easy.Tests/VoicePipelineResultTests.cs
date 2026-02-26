using Ouroboros.Easy;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class VoicePipelineResultTests
{
    [Fact]
    public async Task FailureResult_HasCorrectProperties()
    {
        // Create a failure via pipeline with no topic
        var pipeline = VoicePipeline.Create()
            .WithModel("llama3")
            .Draft();

        // Act
        var result = await pipeline.RunAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.TextOutput.Should().BeNull();
        result.AudioPath.Should().BeNull();
        result.Error.Should().NotBeNull();
        result.Warning.Should().BeNull();
    }

    [Fact]
    public async Task FailureResult_NoStages_HasCorrectError()
    {
        // Arrange
        var pipeline = VoicePipeline.Create()
            .About("test")
            .WithModel("llama3");

        // Act
        var result = await pipeline.RunAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DeferredError_FromVoiceInput_PropagatesOnRun()
    {
        // Arrange - no STT service configured, should produce deferred error
        var pipeline = VoicePipeline.Create()
            .WithModel("llama3")
            .Draft();

        await pipeline.AboutFromVoiceAsync("nonexistent.wav");

        // Act
        var result = await pipeline.RunAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Speech-to-text service");
    }
}
