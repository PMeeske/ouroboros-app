using Ouroboros.CLI.Commands.Handlers;

namespace Ouroboros.Tests.CLI.Commands;

/// <summary>
/// Verifies that command handler extension classes exist and are static.
/// </summary>
[Trait("Category", "Unit")]
public class CommandHandlerExtensionTests
{
    [Fact]
    public void AskCommandHandlerExtensions_IsStaticClass()
    {
        typeof(AskCommandHandlerExtensions).IsAbstract.Should().BeTrue();
        typeof(AskCommandHandlerExtensions).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void AskCommandHandlerExtensions_HasConfigureAskCommand()
    {
        var method = typeof(AskCommandHandlerExtensions).GetMethod("ConfigureAskCommand");
        method.Should().NotBeNull();
    }

    [Theory]
    [InlineData("CognitivePhysicsCommandHandlerExtensions")]
    [InlineData("ImmersiveCommandHandlerExtensions")]
    [InlineData("MeTTaCommandHandlerExtensions")]
    [InlineData("OrchestratorCommandHandlerExtensions")]
    [InlineData("OuroborosCommandHandlerExtensions")]
    [InlineData("PipelineCommandHandlerExtensions")]
    [InlineData("RoomCommandHandlerExtensions")]
    [InlineData("SkillsCommandHandlerExtensions")]
    public void HandlerExtension_ExistsInHandlersNamespace(string typeName)
    {
        var type = typeof(AskCommandHandlerExtensions).Assembly
            .GetType($"Ouroboros.CLI.Commands.Handlers.{typeName}");
        type.Should().NotBeNull($"{typeName} should exist in the Handlers namespace");
        type!.IsAbstract.Should().BeTrue($"{typeName} should be abstract (static)");
        type.IsSealed.Should().BeTrue($"{typeName} should be sealed (static)");
    }
}
