using System.CommandLine;
using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class CognitivePhysicsCommandOptionsTests
{
    [Fact]
    public void OperationOption_HasDescription()
    {
        var options = new CognitivePhysicsCommandOptions();
        options.OperationOption.Description.Should().Contain("CPE operation");
    }

    [Fact]
    public void FocusOption_HasDescription()
    {
        var options = new CognitivePhysicsCommandOptions();
        options.FocusOption.Description.Should().Contain("conceptual domain");
    }

    [Fact]
    public void TargetOption_HasDescription()
    {
        var options = new CognitivePhysicsCommandOptions();
        options.TargetOption.Description.Should().Contain("Target conceptual domain");
    }

    [Fact]
    public void ResourcesOption_HasDescription()
    {
        var options = new CognitivePhysicsCommandOptions();
        options.ResourcesOption.Description.Should().Contain("cognitive resource");
    }

    [Fact]
    public void ChaosIntensityOption_HasDescription()
    {
        var options = new CognitivePhysicsCommandOptions();
        options.ChaosIntensityOption.Description.Should().Contain("Chaos injection");
    }

    [Fact]
    public void JsonOutputOption_HasDescription()
    {
        var options = new CognitivePhysicsCommandOptions();
        options.JsonOutputOption.Description.Should().Contain("JSON");
    }

    [Fact]
    public void VerboseOption_HasDescription()
    {
        var options = new CognitivePhysicsCommandOptions();
        options.VerboseOption.Description.Should().Contain("detailed");
    }

    [Fact]
    public void AddToCommand_AddsAllOptions()
    {
        var options = new CognitivePhysicsCommandOptions();
        var command = new Command("test");

        options.AddToCommand(command);

        command.Options.Should().Contain(options.OperationOption);
        command.Options.Should().Contain(options.FocusOption);
        command.Options.Should().Contain(options.TargetOption);
        command.Options.Should().Contain(options.TargetsOption);
        command.Options.Should().Contain(options.ResourcesOption);
        command.Options.Should().Contain(options.ChaosIntensityOption);
        command.Options.Should().Contain(options.ChaosResourceCostOption);
        command.Options.Should().Contain(options.EvolutionSuccessRateOption);
        command.Options.Should().Contain(options.EvolutionFailureRateOption);
        command.Options.Should().Contain(options.JsonOutputOption);
        command.Options.Should().Contain(options.VerboseOption);
    }
}
