using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class AskRequestTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_SetsQuestion()
    {
        // Arrange & Act
        var request = new AskRequest { Question = "What is AI?" };

        // Assert
        request.Question.Should().Be("What is AI?");
    }

    [Fact]
    public void DefaultValues_OptionalProperties_AreDefaults()
    {
        // Arrange & Act
        var request = new AskRequest { Question = "test" };

        // Assert
        request.UseRag.Should().BeFalse();
        request.SourcePath.Should().BeNull();
        request.Model.Should().BeNull();
        request.Agent.Should().BeFalse();
        request.Temperature.Should().BeNull();
        request.MaxTokens.Should().BeNull();
        request.Endpoint.Should().BeNull();
        request.ApiKey.Should().BeNull();
    }

    [Fact]
    public void AllProperties_SetExplicitly_RetainValues()
    {
        // Arrange & Act
        var request = new AskRequest
        {
            Question = "Complex question",
            UseRag = true,
            SourcePath = "/data/docs",
            Model = "llama3",
            Agent = true,
            Temperature = 0.5f,
            MaxTokens = 4096,
            Endpoint = "http://localhost:11434",
            ApiKey = "test-key"
        };

        // Assert
        request.UseRag.Should().BeTrue();
        request.SourcePath.Should().Be("/data/docs");
        request.Model.Should().Be("llama3");
        request.Agent.Should().BeTrue();
        request.Temperature.Should().Be(0.5f);
        request.MaxTokens.Should().Be(4096);
        request.Endpoint.Should().Be("http://localhost:11434");
        request.ApiKey.Should().Be("test-key");
    }
}
