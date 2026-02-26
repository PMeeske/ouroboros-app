using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class PerformanceDtoTests
{
    [Fact]
    public void DefaultValues_AllProperties_AreZero()
    {
        // Arrange & Act
        var dto = new PerformanceDto();

        // Assert
        dto.OverallSuccessRate.Should().Be(0.0);
        dto.AverageResponseTime.Should().Be(0.0);
        dto.TotalTasks.Should().Be(0);
        dto.SuccessfulTasks.Should().Be(0);
        dto.FailedTasks.Should().Be(0);
    }

    [Fact]
    public void Properties_SetAll_RetainValues()
    {
        // Arrange & Act
        var dto = new PerformanceDto
        {
            OverallSuccessRate = 0.95,
            AverageResponseTime = 250.5,
            TotalTasks = 1000,
            SuccessfulTasks = 950,
            FailedTasks = 50
        };

        // Assert
        dto.OverallSuccessRate.Should().Be(0.95);
        dto.AverageResponseTime.Should().Be(250.5);
        dto.TotalTasks.Should().Be(1000);
        dto.SuccessfulTasks.Should().Be(950);
        dto.FailedTasks.Should().Be(50);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var dto1 = new PerformanceDto { TotalTasks = 100, SuccessfulTasks = 90 };
        var dto2 = new PerformanceDto { TotalTasks = 100, SuccessfulTasks = 90 };

        // Assert
        dto1.Should().Be(dto2);
    }
}
