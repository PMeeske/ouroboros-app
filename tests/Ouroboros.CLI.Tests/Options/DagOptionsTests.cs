using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class DagOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new DagOptions { Command = "snapshot" };

        options.Command.Should().Be("snapshot");
        options.BranchName.Should().BeNull();
        options.EpochNumber.Should().BeNull();
        options.OutputPath.Should().BeNull();
        options.InputPath.Should().BeNull();
        options.DryRun.Should().BeFalse();
        options.MaxAgeDays.Should().BeNull();
        options.MaxCount.Should().BeNull();
        options.Format.Should().Be("summary");
        options.Verbose.Should().BeFalse();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var options = new DagOptions
        {
            Command = "replay",
            BranchName = "main",
            EpochNumber = 42,
            OutputPath = "/tmp/output.json",
            InputPath = "/tmp/input.json",
            DryRun = true,
            MaxAgeDays = 7,
            MaxCount = 100,
            Format = "json",
            Verbose = true
        };

        options.Command.Should().Be("replay");
        options.BranchName.Should().Be("main");
        options.EpochNumber.Should().Be(42);
        options.OutputPath.Should().Be("/tmp/output.json");
        options.InputPath.Should().Be("/tmp/input.json");
        options.DryRun.Should().BeTrue();
        options.MaxAgeDays.Should().Be(7);
        options.MaxCount.Should().Be(100);
        options.Format.Should().Be("json");
        options.Verbose.Should().BeTrue();
    }
}
