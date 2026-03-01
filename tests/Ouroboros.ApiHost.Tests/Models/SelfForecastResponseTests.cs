using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class SelfForecastResponseTests
{
    [Fact]
    public void DefaultValues_AllNullable_AreNull()
    {
        // Arrange & Act
        var response = new SelfForecastResponse();

        // Assert
        response.PendingForecasts.Should().BeNull();
        response.Calibration.Should().BeNull();
        response.RecentAnomalies.Should().BeNull();
    }

    [Fact]
    public void Properties_SetWithData_RetainValues()
    {
        // Arrange
        var forecasts = new List<ForecastDto>
        {
            new() { Id = Guid.NewGuid(), Description = "test", MetricName = "cpu", Status = "Pending" }
        };
        var calibration = new CalibrationDto { TotalForecasts = 10 };
        var anomalies = new List<AnomalyDto>
        {
            new() { MetricName = "memory", Severity = "High" }
        };

        // Act
        var response = new SelfForecastResponse
        {
            PendingForecasts = forecasts,
            Calibration = calibration,
            RecentAnomalies = anomalies
        };

        // Assert
        response.PendingForecasts.Should().HaveCount(1);
        response.Calibration!.TotalForecasts.Should().Be(10);
        response.RecentAnomalies.Should().HaveCount(1);
    }

    [Fact]
    public void Properties_SetWithEmptyLists_RetainEmptyLists()
    {
        // Arrange & Act
        var response = new SelfForecastResponse
        {
            PendingForecasts = new List<ForecastDto>(),
            RecentAnomalies = new List<AnomalyDto>()
        };

        // Assert
        response.PendingForecasts.Should().BeEmpty();
        response.RecentAnomalies.Should().BeEmpty();
    }
}
