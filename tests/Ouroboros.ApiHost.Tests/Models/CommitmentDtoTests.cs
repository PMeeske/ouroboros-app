using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class CommitmentDtoTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_SetsValues()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var dto = new CommitmentDto
        {
            Id = id,
            Description = "Complete task",
            Status = "InProgress"
        };

        // Assert
        dto.Id.Should().Be(id);
        dto.Description.Should().Be("Complete task");
        dto.Status.Should().Be("InProgress");
    }

    [Fact]
    public void Properties_SetAll_RetainValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var deadline = DateTime.UtcNow.AddDays(7);

        // Act
        var dto = new CommitmentDto
        {
            Id = id,
            Description = "Deploy feature",
            Deadline = deadline,
            Priority = 0.9,
            Status = "Pending",
            ProgressPercent = 75.5
        };

        // Assert
        dto.Deadline.Should().Be(deadline);
        dto.Priority.Should().Be(0.9);
        dto.ProgressPercent.Should().Be(75.5);
    }

    [Fact]
    public void DefaultValues_NumericProperties_AreZero()
    {
        // Arrange & Act
        var dto = new CommitmentDto
        {
            Id = Guid.Empty,
            Description = "test",
            Status = "New"
        };

        // Assert
        dto.Priority.Should().Be(0.0);
        dto.ProgressPercent.Should().Be(0.0);
        dto.Deadline.Should().Be(default);
    }
}
