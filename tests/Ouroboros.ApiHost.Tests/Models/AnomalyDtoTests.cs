using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class AnomalyDtoTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_SetsValues()
    {
        // Arrange & Act
        var dto = new AnomalyDto
        {
            MetricName = "cpu_usage",
            Severity = "High"
        };

        // Assert
        dto.MetricName.Should().Be("cpu_usage");
        dto.Severity.Should().Be("High");
    }

    [Fact]
    public void Properties_SetAndGet_ReturnExpectedValues()
    {
        // Arrange
        var detectedAt = new DateTime(2025, 6, 15, 10, 30, 0);

        // Act
        var dto = new AnomalyDto
        {
            MetricName = "memory_usage",
            ObservedValue = 95.5,
            ExpectedValue = 70.0,
            Deviation = 25.5,
            Severity = "Critical",
            DetectedAt = detectedAt
        };

        // Assert
        dto.ObservedValue.Should().Be(95.5);
        dto.ExpectedValue.Should().Be(70.0);
        dto.Deviation.Should().Be(25.5);
        dto.DetectedAt.Should().Be(detectedAt);
    }

    [Fact]
    public void DefaultValues_NumericProperties_AreZero()
    {
        // Arrange & Act
        var dto = new AnomalyDto
        {
            MetricName = "test",
            Severity = "Low"
        };

        // Assert
        dto.ObservedValue.Should().Be(0.0);
        dto.ExpectedValue.Should().Be(0.0);
        dto.Deviation.Should().Be(0.0);
        dto.DetectedAt.Should().Be(default);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var dto1 = new AnomalyDto
        {
            MetricName = "cpu",
            Severity = "High",
            ObservedValue = 90.0,
            ExpectedValue = 50.0,
            Deviation = 40.0
        };
        var dto2 = new AnomalyDto
        {
            MetricName = "cpu",
            Severity = "High",
            ObservedValue = 90.0,
            ExpectedValue = 50.0,
            Deviation = 40.0
        };

        // Assert
        dto1.Should().Be(dto2);
    }
}
