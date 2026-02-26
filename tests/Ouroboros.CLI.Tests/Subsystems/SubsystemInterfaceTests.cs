using Ouroboros.CLI.Subsystems;

namespace Ouroboros.Tests.CLI.Subsystems;

/// <summary>
/// Verifies that all subsystem interface types exist and extend IAgentSubsystem.
/// </summary>
[Trait("Category", "Unit")]
public class SubsystemInterfaceTests
{
    [Fact]
    public void IAgentSubsystem_ExtendsIAsyncDisposable()
    {
        typeof(IAgentSubsystem).Should().Implement<IAsyncDisposable>();
    }

    [Fact]
    public void IAuthSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(IAuthSubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void IModelSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(IModelSubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void IToolSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(IToolSubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void IMemorySubsystem_ExtendsIAgentSubsystem()
    {
        typeof(IMemorySubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void ICognitiveSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(ICognitiveSubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void IAutonomySubsystem_ExtendsIAgentSubsystem()
    {
        typeof(IAutonomySubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void IEmbodimentSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(IEmbodimentSubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void ILocalizationSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(ILocalizationSubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void ILanguageSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(ILanguageSubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void ISelfAssemblySubsystem_ExtendsIAgentSubsystem()
    {
        typeof(ISelfAssemblySubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void IPipeProcessingSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(IPipeProcessingSubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void IChatSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(IChatSubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void ICommandRoutingSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(ICommandRoutingSubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void ISwarmSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(ISwarmSubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void IVoiceSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(IVoiceSubsystem).Should().Implement<IAgentSubsystem>();
    }

    [Fact]
    public void IImmersiveSubsystem_ExtendsIAgentSubsystem()
    {
        typeof(IImmersiveSubsystem).Should().Implement<IAgentSubsystem>();
    }
}
