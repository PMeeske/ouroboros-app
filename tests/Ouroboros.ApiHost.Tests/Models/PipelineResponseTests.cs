using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class PipelineResponseTests
{
    [Fact]
    public void Constructor_WithRequiredResult_SetsResult()
    {
        // Arrange & Act
        var response = new PipelineResponse { Result = "Pipeline output" };

        // Assert
        response.Result.Should().Be("Pipeline output");
    }

    [Fact]
    public void FinalState_WhenNotSet_IsNull()
    {
        // Arrange & Act
        var response = new PipelineResponse { Result = "test" };

        // Assert
        response.FinalState.Should().BeNull();
    }

    [Fact]
    public void AllProperties_SetExplicitly_RetainValues()
    {
        // Arrange & Act
        var response = new PipelineResponse
        {
            Result = "Completed output",
            FinalState = "Completed"
        };

        // Assert
        response.Result.Should().Be("Completed output");
        response.FinalState.Should().Be("Completed");
    }
}
