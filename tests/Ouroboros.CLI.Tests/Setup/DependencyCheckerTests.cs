using Ouroboros.CLI.Setup;

namespace Ouroboros.Tests.CLI.Setup;

[Trait("Category", "Unit")]
public class DependencyCheckerTests
{
    [Fact]
    public void DependencyChecker_IsStaticClass()
    {
        typeof(DependencyChecker).IsAbstract.Should().BeTrue();
        typeof(DependencyChecker).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void EnsureOllamaIsRunningAsync_MethodExists()
    {
        var method = typeof(DependencyChecker).GetMethod("EnsureOllamaIsRunningAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<bool>));
    }

    [Fact]
    public void EnsureMeTTaIsAvailableAsync_MethodExists()
    {
        var method = typeof(DependencyChecker).GetMethod("EnsureMeTTaIsAvailableAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
    }
}
