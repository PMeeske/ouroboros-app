using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class MaintenanceOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new MaintenanceOptions { Command = "compact" };

        options.Command.Should().Be("compact");
        options.ArchiveAgeDays.Should().Be(30);
        options.ArchivePath.Should().BeNull();
        options.TaskName.Should().BeNull();
        options.ScheduleHours.Should().Be(24);
        options.AlertId.Should().BeNull();
        options.Resolution.Should().BeNull();
        options.Format.Should().Be("summary");
        options.Limit.Should().Be(50);
        options.UnresolvedOnly.Should().BeTrue();
        options.Verbose.Should().BeFalse();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var options = new MaintenanceOptions
        {
            Command = "archive",
            ArchiveAgeDays = 7,
            ArchivePath = "/tmp/archive",
            TaskName = "daily-compact",
            ScheduleHours = 12,
            AlertId = "abc-123",
            Resolution = "resolved manually",
            Format = "json",
            Limit = 10,
            UnresolvedOnly = false,
            Verbose = true
        };

        options.Command.Should().Be("archive");
        options.ArchiveAgeDays.Should().Be(7);
        options.ArchivePath.Should().Be("/tmp/archive");
        options.TaskName.Should().Be("daily-compact");
        options.ScheduleHours.Should().Be(12);
        options.AlertId.Should().Be("abc-123");
        options.Resolution.Should().Be("resolved manually");
        options.Format.Should().Be("json");
        options.Limit.Should().Be(10);
        options.UnresolvedOnly.Should().BeFalse();
        options.Verbose.Should().BeTrue();
    }
}
