using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class EnvironmentOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new EnvironmentOptions();

        options.Command.Should().BeNull();
        options.Environment.Should().Be("gridworld");
        options.Policy.Should().Be("epsilon-greedy");
        options.Epsilon.Should().Be(0.1);
        options.Episodes.Should().Be(10);
        options.MaxSteps.Should().Be(100);
        options.Width.Should().Be(5);
        options.Height.Should().Be(5);
        options.Verbose.Should().BeFalse();
        options.OutputFile.Should().BeNull();
        options.InputFile.Should().BeNull();
        options.Seed.Should().BeNull();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var options = new EnvironmentOptions
        {
            Command = "step",
            Environment = "maze",
            Policy = "random",
            Epsilon = 0.5,
            Episodes = 50,
            MaxSteps = 200,
            Width = 10,
            Height = 10,
            Verbose = true,
            OutputFile = "/tmp/episodes.json",
            InputFile = "/tmp/replay.json",
            Seed = 42
        };

        options.Command.Should().Be("step");
        options.Environment.Should().Be("maze");
        options.Policy.Should().Be("random");
        options.Epsilon.Should().Be(0.5);
        options.Episodes.Should().Be(50);
        options.MaxSteps.Should().Be(200);
        options.Width.Should().Be(10);
        options.Height.Should().Be(10);
        options.Verbose.Should().BeTrue();
        options.OutputFile.Should().Be("/tmp/episodes.json");
        options.InputFile.Should().Be("/tmp/replay.json");
        options.Seed.Should().Be(42);
    }
}
