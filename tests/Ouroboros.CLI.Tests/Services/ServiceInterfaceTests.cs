using Ouroboros.CLI.Services;

namespace Ouroboros.Tests.CLI.Services;

/// <summary>
/// Verifies that all CLI service interfaces are properly defined.
/// </summary>
[Trait("Category", "Unit")]
public class ServiceInterfaceTests
{
    [Fact]
    public void IAskService_HasAskAsyncMethod()
    {
        typeof(IAskService).GetMethod("AskAsync").Should().NotBeNull();
    }

    [Fact]
    public void ICognitivePhysicsService_IsInterface()
    {
        typeof(ICognitivePhysicsService).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IImmersiveModeService_IsInterface()
    {
        typeof(IImmersiveModeService).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IMeTTaService_IsInterface()
    {
        typeof(IMeTTaService).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IOrchestratorService_IsInterface()
    {
        typeof(IOrchestratorService).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IOuroborosAgentService_IsInterface()
    {
        typeof(IOuroborosAgentService).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IPipelineService_IsInterface()
    {
        typeof(IPipelineService).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IRoomModeService_IsInterface()
    {
        typeof(IRoomModeService).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void ISkillsService_IsInterface()
    {
        typeof(ISkillsService).IsInterface.Should().BeTrue();
    }
}
