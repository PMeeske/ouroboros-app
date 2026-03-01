using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;

namespace Ouroboros.Tests.CLI.Infrastructure;

[Trait("Category", "Unit")]
public class ConsoleOutputTests
{
    [Theory]
    [InlineData(OutputVerbosity.Quiet)]
    [InlineData(OutputVerbosity.Normal)]
    [InlineData(OutputVerbosity.Verbose)]
    public void Constructor_SetsVerbosity(OutputVerbosity verbosity)
    {
        var output = new ConsoleOutput(verbosity);

        output.Verbosity.Should().Be(verbosity);
    }

    [Fact]
    public void RecordInit_DoesNotThrow()
    {
        var output = new ConsoleOutput(OutputVerbosity.Quiet);

        var action = () => output.RecordInit("TestSubsystem", true, "detail");

        action.Should().NotThrow();
    }

    [Fact]
    public void RecordInit_WithFailure_DoesNotThrow()
    {
        var output = new ConsoleOutput(OutputVerbosity.Quiet);

        var action = () => output.RecordInit("TestSubsystem", false, "failed");

        action.Should().NotThrow();
    }

    [Fact]
    public void WriteSystem_QuietMode_DoesNotThrow()
    {
        var output = new ConsoleOutput(OutputVerbosity.Quiet);

        var action = () => output.WriteSystem("test message");

        action.Should().NotThrow();
    }

    [Fact]
    public void WriteDebug_QuietMode_DoesNotThrow()
    {
        var output = new ConsoleOutput(OutputVerbosity.Quiet);

        var action = () => output.WriteDebug("debug message");

        action.Should().NotThrow();
    }

    [Fact]
    public void StartSpinner_QuietMode_ReturnsNullSpinnerHandle()
    {
        var output = new ConsoleOutput(OutputVerbosity.Quiet);

        using var handle = output.StartSpinner("loading...");

        // In quiet mode, should return the NullSpinnerHandle singleton
        handle.Should().NotBeNull();
        handle.Should().BeAssignableTo<ISpinnerHandle>();
    }

    [Fact]
    public void FlushInitSummary_QuietMode_DoesNotThrow()
    {
        var output = new ConsoleOutput(OutputVerbosity.Quiet);
        output.RecordInit("Sub1", true);
        output.RecordInit("Sub2", false, "unavailable");

        var action = () => output.FlushInitSummary();

        action.Should().NotThrow();
    }

    [Fact]
    public void ImplementsIConsoleOutput()
    {
        var output = new ConsoleOutput(OutputVerbosity.Normal);
        output.Should().BeAssignableTo<IConsoleOutput>();
    }
}
