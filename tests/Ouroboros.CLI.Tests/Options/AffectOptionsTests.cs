using Ouroboros.CLI.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class AffectOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new AffectOptions { Command = "show" };

        options.Command.Should().Be("show");
        options.SignalType.Should().BeNull();
        options.SignalValue.Should().BeNull();
        options.RuleName.Should().BeNull();
        options.LowerBound.Should().BeNull();
        options.UpperBound.Should().BeNull();
        options.TargetValue.Should().BeNull();
        options.DetectStress.Should().BeFalse();
        options.OutputFormat.Should().Be("table");
        options.Verbose.Should().BeFalse();
        options.OutputPath.Should().BeNull();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var options = new AffectOptions
        {
            Command = "signal",
            SignalType = "stress",
            SignalValue = 0.8,
            RuleName = "escalation",
            LowerBound = 0.0,
            UpperBound = 1.0,
            TargetValue = 0.5,
            DetectStress = true,
            OutputFormat = "json",
            Verbose = true,
            OutputPath = "/tmp/output.json"
        };

        options.Command.Should().Be("signal");
        options.SignalType.Should().Be("stress");
        options.SignalValue.Should().Be(0.8);
        options.RuleName.Should().Be("escalation");
        options.LowerBound.Should().Be(0.0);
        options.UpperBound.Should().Be(1.0);
        options.TargetValue.Should().Be(0.5);
        options.DetectStress.Should().BeTrue();
        options.OutputFormat.Should().Be("json");
        options.Verbose.Should().BeTrue();
        options.OutputPath.Should().Be("/tmp/output.json");
    }
}
