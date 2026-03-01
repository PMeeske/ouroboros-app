using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class AskResponseTests
{
    [Fact]
    public void Constructor_WithRequiredAnswer_SetsAnswer()
    {
        // Arrange & Act
        var response = new AskResponse { Answer = "The answer is 42" };

        // Assert
        response.Answer.Should().Be("The answer is 42");
    }

    [Fact]
    public void Model_WhenNotSet_IsNull()
    {
        // Arrange & Act
        var response = new AskResponse { Answer = "test" };

        // Assert
        response.Model.Should().BeNull();
    }

    [Fact]
    public void AllProperties_SetExplicitly_RetainValues()
    {
        // Arrange & Act
        var response = new AskResponse
        {
            Answer = "Generated text",
            Model = "phi3:mini"
        };

        // Assert
        response.Answer.Should().Be("Generated text");
        response.Model.Should().Be("phi3:mini");
    }
}
