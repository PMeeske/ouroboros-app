using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class CalibrationDtoTests
{
    [Fact]
    public void DefaultValues_AllNumericProperties_AreZero()
    {
        // Arrange & Act
        var dto = new CalibrationDto();

        // Assert
        dto.TotalForecasts.Should().Be(0);
        dto.AverageConfidence.Should().Be(0.0);
        dto.AverageAccuracy.Should().Be(0.0);
        dto.BrierScore.Should().Be(0.0);
        dto.CalibrationError.Should().Be(0.0);
    }

    [Fact]
    public void Properties_SetAndGet_ReturnExpectedValues()
    {
        // Arrange & Act
        var dto = new CalibrationDto
        {
            TotalForecasts = 100,
            AverageConfidence = 0.85,
            AverageAccuracy = 0.90,
            BrierScore = 0.12,
            CalibrationError = 0.05
        };

        // Assert
        dto.TotalForecasts.Should().Be(100);
        dto.AverageConfidence.Should().Be(0.85);
        dto.AverageAccuracy.Should().Be(0.90);
        dto.BrierScore.Should().Be(0.12);
        dto.CalibrationError.Should().Be(0.05);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var dto1 = new CalibrationDto { TotalForecasts = 50, AverageConfidence = 0.8 };
        var dto2 = new CalibrationDto { TotalForecasts = 50, AverageConfidence = 0.8 };

        // Assert
        dto1.Should().Be(dto2);
    }
}
