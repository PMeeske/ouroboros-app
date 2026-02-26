using Ouroboros.CLI.Commands.Options;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class IComposableOptionsTests
{
    [Fact]
    public void IComposableOptions_IsInterface()
    {
        typeof(IComposableOptions).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IComposableOptions_HasAddToCommandMethod()
    {
        var method = typeof(IComposableOptions).GetMethod("AddToCommand");
        method.Should().NotBeNull();
    }

    [Fact]
    public void AgentLoopOptions_ImplementsIComposableOptions()
    {
        typeof(AgentLoopOptions).Should().Implement<IComposableOptions>();
    }

    [Fact]
    public void CollectiveOptions_ImplementsIComposableOptions()
    {
        typeof(CollectiveOptions).Should().Implement<IComposableOptions>();
    }

    [Fact]
    public void CommandVoiceOptions_ImplementsIComposableOptions()
    {
        typeof(CommandVoiceOptions).Should().Implement<IComposableOptions>();
    }

    [Fact]
    public void DiagnosticOptions_ImplementsIComposableOptions()
    {
        typeof(DiagnosticOptions).Should().Implement<IComposableOptions>();
    }

    [Fact]
    public void EmbeddingOptions_ImplementsIComposableOptions()
    {
        typeof(EmbeddingOptions).Should().Implement<IComposableOptions>();
    }

    [Fact]
    public void EndpointOptions_ImplementsIComposableOptions()
    {
        typeof(EndpointOptions).Should().Implement<IComposableOptions>();
    }

    [Fact]
    public void MultiModelOptions_ImplementsIComposableOptions()
    {
        typeof(MultiModelOptions).Should().Implement<IComposableOptions>();
    }
}
