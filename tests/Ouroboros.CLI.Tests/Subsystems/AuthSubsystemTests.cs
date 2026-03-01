using Ouroboros.CLI.Subsystems;

namespace Ouroboros.Tests.CLI.Subsystems;

[Trait("Category", "Unit")]
public class AuthSubsystemTests
{
    [Fact]
    public void Name_IsAuth()
    {
        var sub = new AuthSubsystem();
        sub.Name.Should().Be("Auth");
    }

    [Fact]
    public void IsInitialized_InitiallyFalse()
    {
        var sub = new AuthSubsystem();
        sub.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void ImplementsIAuthSubsystem()
    {
        var sub = new AuthSubsystem();
        sub.Should().BeAssignableTo<IAuthSubsystem>();
    }

    [Fact]
    public void ImplementsIAgentSubsystem()
    {
        var sub = new AuthSubsystem();
        sub.Should().BeAssignableTo<IAgentSubsystem>();
    }

    [Fact]
    public void ImplementsIAsyncDisposable()
    {
        var sub = new AuthSubsystem();
        sub.Should().BeAssignableTo<IAsyncDisposable>();
    }

    [Fact]
    public void IsSealed()
    {
        typeof(AuthSubsystem).IsSealed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        var sub = new AuthSubsystem();
        var action = async () => await sub.DisposeAsync();
        await action.Should().NotThrowAsync();
    }
}
