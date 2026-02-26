using Ouroboros.CLI.Abstractions;

namespace Ouroboros.Tests.CLI.Abstractions;

[Trait("Category", "Unit")]
public class AbstractionInterfaceTests
{
    [Fact]
    public void IAgentFacade_IsInterface()
    {
        typeof(IAgentFacade).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void ICommandHandler_IsInterface()
    {
        typeof(ICommandHandler).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void ICommandHandler_HasHandleAsyncMethod()
    {
        typeof(ICommandHandler).GetMethod("HandleAsync").Should().NotBeNull();
    }

    [Fact]
    public void ICommandHandlerGeneric_IsInterface()
    {
        typeof(ICommandHandler<>).IsInterface.Should().BeTrue();
    }
}
