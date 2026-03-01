using Ouroboros.CLI.Subsystems;

namespace Ouroboros.Tests.CLI.Subsystems;

[Trait("Category", "Unit")]
public class ChatSubsystemTests
{
    [Fact]
    public void Name_IsChat()
    {
        var sub = new ChatSubsystem();
        sub.Name.Should().Be("Chat");
    }

    [Fact]
    public void IsInitialized_InitiallyFalse()
    {
        var sub = new ChatSubsystem();
        sub.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void ImplementsIChatSubsystem()
    {
        var sub = new ChatSubsystem();
        sub.Should().BeAssignableTo<IChatSubsystem>();
    }

    [Fact]
    public void ImplementsIAgentSubsystem()
    {
        var sub = new ChatSubsystem();
        sub.Should().BeAssignableTo<IAgentSubsystem>();
    }

    [Fact]
    public void IsSealed()
    {
        typeof(ChatSubsystem).IsSealed.Should().BeTrue();
    }
}
