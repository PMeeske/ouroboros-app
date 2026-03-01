using Ouroboros.Easy.Localization;

namespace Ouroboros.Tests.Localization;

[Trait("Category", "Unit")]
public sealed class LanguageResourcesTests
{
    [Fact]
    public void DefaultValues_AllProperties_AreEmptyStrings()
    {
        // Arrange & Act
        var resources = new LanguageResources();

        // Assert
        resources.WelcomeMessage.Should().BeEmpty();
        resources.PipelineStarting.Should().BeEmpty();
        resources.PipelineCompleted.Should().BeEmpty();
        resources.PipelineFailed.Should().BeEmpty();
        resources.DraftStage.Should().BeEmpty();
        resources.CritiqueStage.Should().BeEmpty();
        resources.ImproveStage.Should().BeEmpty();
        resources.SummarizeStage.Should().BeEmpty();
        resources.VoiceInputProcessing.Should().BeEmpty();
        resources.VoiceOutputGenerating.Should().BeEmpty();
        resources.ErrorOccurred.Should().BeEmpty();
        resources.TopicRequired.Should().BeEmpty();
        resources.ModelRequired.Should().BeEmpty();
        resources.StageRequired.Should().BeEmpty();
    }

    [Fact]
    public void Properties_SetAndGet_RetainValues()
    {
        // Arrange & Act
        var resources = new LanguageResources
        {
            WelcomeMessage = "Hello",
            PipelineStarting = "Starting...",
            PipelineCompleted = "Done!",
            PipelineFailed = "Failed: {0}",
            DraftStage = "Drafting",
            CritiqueStage = "Critiquing",
            ImproveStage = "Improving",
            SummarizeStage = "Summarizing",
            VoiceInputProcessing = "Processing voice...",
            VoiceOutputGenerating = "Generating voice...",
            ErrorOccurred = "Error: {0}",
            TopicRequired = "Need topic",
            ModelRequired = "Need model",
            StageRequired = "Need stage"
        };

        // Assert
        resources.WelcomeMessage.Should().Be("Hello");
        resources.PipelineFailed.Should().Be("Failed: {0}");
        resources.StageRequired.Should().Be("Need stage");
    }

    [Fact]
    public void AllProperties_AreSettable()
    {
        // Arrange
        var resources = new LanguageResources();

        // Act
        resources.WelcomeMessage = "Test";

        // Assert
        resources.WelcomeMessage.Should().Be("Test");
    }
}
