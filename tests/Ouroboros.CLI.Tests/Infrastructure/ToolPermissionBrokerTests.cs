using Ouroboros.CLI.Infrastructure;

namespace Ouroboros.Tests.CLI.Infrastructure;

[Trait("Category", "Unit")]
public class ToolPermissionBrokerTests
{
    [Fact]
    public void SkipAll_DefaultsToFalse()
    {
        var broker = new ToolPermissionBroker();

        broker.SkipAll.Should().BeFalse();
    }

    [Fact]
    public async Task RequestAsync_WhenSkipAll_ReturnsAllow()
    {
        var broker = new ToolPermissionBroker { SkipAll = true };

        var result = await broker.RequestAsync("tool", "action");

        result.Should().Be(PermissionAction.Allow);
    }

    [Fact]
    public async Task RequestAsync_WhenCancelled_ReturnsDeny()
    {
        var broker = new ToolPermissionBroker();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await broker.RequestAsync("tool", "action", ct: cts.Token);

        result.Should().Be(PermissionAction.Deny);
    }
}
