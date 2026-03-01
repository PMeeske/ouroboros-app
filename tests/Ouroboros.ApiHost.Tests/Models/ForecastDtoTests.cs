using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class ForecastDtoTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_SetsValues()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var dto = new ForecastDto
        {
            Id = id,
            Description = "CPU will spike",
            MetricName = "cpu_usage",
            Status = "Pending"
        };

        // Assert
        dto.Id.Should().Be(id);
        dto.Description.Should().Be("CPU will spike");
        dto.MetricName.Should().Be("cpu_usage");
        dto.Status.Should().Be("Pending");
    }

    [Fact]
    public void Properties_SetAll_RetainValues()
    {
        // Arrange
        var targetTime = DateTime.UtcNow.AddHours(6);

        // Act
        var dto = new ForecastDto
        {
            Id = Guid.NewGuid(),
            Description = "Memory forecast",
            MetricName = "memory_gb",
            PredictedValue = 12.5,
            Confidence = 0.85,
            TargetTime = targetTime,
            Status = "Active"
        };

        // Assert
        dto.PredictedValue.Should().Be(12.5);
        dto.Confidence.Should().Be(0.85);
        dto.TargetTime.Should().Be(targetTime);
    }

    [Fact]
    public void DefaultValues_NumericProperties_AreZero()
    {
        // Arrange & Act
        var dto = new ForecastDto
        {
            Id = Guid.Empty,
            Description = "test",
            MetricName = "test",
            Status = "New"
        };

        // Assert
        dto.PredictedValue.Should().Be(0.0);
        dto.Confidence.Should().Be(0.0);
        dto.TargetTime.Should().Be(default);
    }
}
