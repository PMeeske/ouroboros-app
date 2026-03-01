using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Models;

[Trait("Category", "Unit")]
public class OutputVerbosityTests
{
    [Theory]
    [InlineData(OutputVerbosity.Quiet)]
    [InlineData(OutputVerbosity.Normal)]
    [InlineData(OutputVerbosity.Verbose)]
    public void OutputVerbosity_ContainsExpectedValues(OutputVerbosity verbosity)
    {
        Enum.IsDefined(typeof(OutputVerbosity), verbosity).Should().BeTrue();
    }

    [Fact]
    public void OutputVerbosity_HasExactlyThreeMembers()
    {
        var values = Enum.GetValues<OutputVerbosity>();
        values.Should().HaveCount(3);
    }

    [Fact]
    public void OutputVerbosity_OrderIsCorrect()
    {
        ((int)OutputVerbosity.Quiet).Should().BeLessThan((int)OutputVerbosity.Normal);
        ((int)OutputVerbosity.Normal).Should().BeLessThan((int)OutputVerbosity.Verbose);
    }
}
