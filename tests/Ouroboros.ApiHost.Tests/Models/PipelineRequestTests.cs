using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class PipelineRequestTests
{
    [Fact]
    public void Constructor_WithRequiredDsl_SetsDsl()
    {
        // Arrange & Act
        var request = new PipelineRequest { Dsl = "SetTopic('AI') | UseDraft" };

        // Assert
        request.Dsl.Should().Be("SetTopic('AI') | UseDraft");
    }

    [Fact]
    public void DefaultValues_OptionalProperties_AreDefaults()
    {
        // Arrange & Act
        var request = new PipelineRequest { Dsl = "test" };

        // Assert
        request.Model.Should().BeNull();
        request.Debug.Should().BeFalse();
        request.Temperature.Should().BeNull();
        request.MaxTokens.Should().BeNull();
        request.Endpoint.Should().BeNull();
        request.ApiKey.Should().BeNull();
    }

    [Fact]
    public void AllProperties_SetExplicitly_RetainValues()
    {
        // Arrange & Act
        var request = new PipelineRequest
        {
            Dsl = "SetTopic('AI') | UseDraft | UseCritique",
            Model = "phi3:mini",
            Debug = true,
            Temperature = 0.3f,
            MaxTokens = 8000,
            Endpoint = "https://api.example.com",
            ApiKey = "my-key"
        };

        // Assert
        request.Model.Should().Be("phi3:mini");
        request.Debug.Should().BeTrue();
        request.Temperature.Should().Be(0.3f);
        request.MaxTokens.Should().Be(8000);
    }
}
