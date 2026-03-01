using Ouroboros.Easy;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class VoicePipelineTests
{
    [Fact]
    public void Create_ReturnsNewInstance()
    {
        // Act
        var pipeline = VoicePipeline.Create();

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void WithLanguage_NullLanguageCode_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = VoicePipeline.Create();

        // Act
        var act = () => pipeline.WithLanguage(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("languageCode");
    }

    [Fact]
    public void WithVoice_NullVoiceName_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = VoicePipeline.Create();

        // Act
        var act = () => pipeline.WithVoice(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("voiceName");
    }

    [Fact]
    public void WithSpeechToText_NullService_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = VoicePipeline.Create();

        // Act
        var act = () => pipeline.WithSpeechToText(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sttService");
    }

    [Fact]
    public void WithTextToSpeech_NullService_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = VoicePipeline.Create();

        // Act
        var act = () => pipeline.WithTextToSpeech(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ttsService");
    }

    [Fact]
    public void ToDSL_DefaultConfig_ShowsDefaultValues()
    {
        // Arrange
        var pipeline = VoicePipeline.Create()
            .About("test")
            .WithModel("llama3")
            .Draft();

        // Act
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("Language: en");
        dsl.Should().Contain("Voice: alloy");
        dsl.Should().Contain("Voice Input: disabled");
        dsl.Should().Contain("Voice Output: disabled");
        dsl.Should().Contain("STT Service: none");
        dsl.Should().Contain("TTS Service: none");
    }

    [Fact]
    public void ToDSL_WithLanguage_ShowsConfiguredLanguage()
    {
        // Arrange
        var pipeline = VoicePipeline.Create()
            .About("test")
            .WithModel("llama3")
            .Draft()
            .WithLanguage("de");

        // Act
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("Language: de");
    }

    [Fact]
    public void ToDSL_WithVoice_ShowsConfiguredVoice()
    {
        // Arrange
        var pipeline = VoicePipeline.Create()
            .About("test")
            .WithModel("llama3")
            .Draft()
            .WithVoice("nova");

        // Act
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("Voice: nova");
    }

    [Fact]
    public async Task RunAsync_NoTopic_ReturnsFailure()
    {
        // Arrange
        var pipeline = VoicePipeline.Create()
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
        var pipeline = VoicePipeline.Create()
            .About("test")
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
        var pipeline = VoicePipeline.Create()
            .About("test")
            .WithModel("llama3");

        // Act
        var result = await pipeline.RunAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task AboutFromVoiceAsync_NoSttService_ReturnsDeferredError()
    {
        // Arrange
        var pipeline = VoicePipeline.Create()
            .WithModel("llama3")
            .Draft();

        // Act
        await pipeline.AboutFromVoiceAsync("test.wav");
        var result = await pipeline.RunAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Speech-to-text service must be configured");
    }

    [Fact]
    public void FluentChaining_AllMethods_ReturnSameInstance()
    {
        // Arrange
        var pipeline = VoicePipeline.Create();

        // Act
        var result = pipeline
            .About("test")
            .Draft()
            .Critique()
            .Improve()
            .Summarize()
            .WithModel("llama3")
            .WithTemperature(0.5)
            .WithLanguage("de")
            .WithVoice("echo");

        // Assert
        result.Should().BeSameAs(pipeline);
    }
}
