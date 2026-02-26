using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Consciousness;

[Trait("Category", "Unit")]
public class DriveStateTests
{
    [Fact]
    public void Increase_ShouldRaiseLevel()
    {
        var drive = new DriveState("curiosity", 0.5, 0.5, 0.01,
            new[] { "exploration" }, DateTime.UtcNow);

        var result = drive.Increase(0.2);

        result.Level.Should().Be(0.7);
    }

    [Fact]
    public void Increase_ShouldNotExceed1()
    {
        var drive = new DriveState("curiosity", 0.9, 0.5, 0.01,
            new[] { "exploration" }, DateTime.UtcNow);

        var result = drive.Increase(0.5);

        result.Level.Should().Be(1.0);
    }

    [Fact]
    public void Decrease_ShouldLowerLevel()
    {
        var drive = new DriveState("curiosity", 0.5, 0.5, 0.01,
            new[] { "exploration" }, DateTime.UtcNow);

        var result = drive.Decrease(0.2);

        result.Level.Should().Be(0.3);
    }

    [Fact]
    public void Decrease_ShouldNotGoBelowZero()
    {
        var drive = new DriveState("curiosity", 0.1, 0.5, 0.01,
            new[] { "exploration" }, DateTime.UtcNow);

        var result = drive.Decrease(0.5);

        result.Level.Should().Be(0.0);
    }

    [Fact]
    public void CreateDefaultDrives_ShouldReturn5Drives()
    {
        var drives = DriveState.CreateDefaultDrives();

        drives.Should().HaveCount(5);
        drives.Select(d => d.Name).Should().Contain("curiosity");
        drives.Select(d => d.Name).Should().Contain("social");
        drives.Select(d => d.Name).Should().Contain("achievement");
        drives.Select(d => d.Name).Should().Contain("novelty");
        drives.Select(d => d.Name).Should().Contain("harmony");
    }

    [Fact]
    public void UpdateWithDecay_ShouldMoveTowardBaseline()
    {
        var drive = new DriveState("curiosity", 0.9, 0.5, 0.1,
            new[] { "exploration" }, DateTime.UtcNow);

        var result = drive.UpdateWithDecay(TimeSpan.FromMinutes(5));

        result.Level.Should().BeLessThan(0.9);
    }
}
