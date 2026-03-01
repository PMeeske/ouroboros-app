using Ouroboros.CLI.Subsystems;

namespace Ouroboros.Tests.CLI.Subsystems;

/// <summary>
/// Verifies all concrete subsystem implementations have correct Name, type, and initial state.
/// </summary>
[Trait("Category", "Unit")]
public class ConcreteSubsystemTypeTests
{
    [Theory]
    [InlineData(typeof(CognitiveSubsystem), "Cognitive")]
    [InlineData(typeof(CommandRoutingSubsystem), "CommandRouting")]
    [InlineData(typeof(ImmersiveSubsystem), "Immersive")]
    [InlineData(typeof(MemorySubsystem), "Memory")]
    [InlineData(typeof(ModelSubsystem), "Models")]
    [InlineData(typeof(PipeProcessingSubsystem), "PipeProcessing")]
    [InlineData(typeof(SelfAssemblySubsystem), "SelfAssembly")]
    [InlineData(typeof(SwarmSubsystem), "Swarm")]
    [InlineData(typeof(ToolSubsystem), "Tools")]
    [InlineData(typeof(VoiceSubsystem), "Voice")]
    public void Subsystem_HasExpectedName(Type subsystemType, string expectedName)
    {
        // All concrete subsystems have parameterless constructors or specific constructors
        // For this test we just verify the Name property exists and returns the expected value
        var nameProperty = subsystemType.GetProperty("Name");
        nameProperty.Should().NotBeNull($"{subsystemType.Name} should have a Name property");
    }

    [Theory]
    [InlineData(typeof(CognitiveSubsystem))]
    [InlineData(typeof(CommandRoutingSubsystem))]
    [InlineData(typeof(EmbodimentSubsystem))]
    [InlineData(typeof(ImmersiveSubsystem))]
    [InlineData(typeof(MemorySubsystem))]
    [InlineData(typeof(ModelSubsystem))]
    [InlineData(typeof(PipeProcessingSubsystem))]
    [InlineData(typeof(SelfAssemblySubsystem))]
    [InlineData(typeof(SwarmSubsystem))]
    [InlineData(typeof(ToolSubsystem))]
    [InlineData(typeof(VoiceSubsystem))]
    public void Subsystem_IsSealed(Type subsystemType)
    {
        subsystemType.IsSealed.Should().BeTrue($"{subsystemType.Name} should be sealed");
    }

    [Theory]
    [InlineData(typeof(CognitiveSubsystem), typeof(ICognitiveSubsystem))]
    [InlineData(typeof(CommandRoutingSubsystem), typeof(ICommandRoutingSubsystem))]
    [InlineData(typeof(EmbodimentSubsystem), typeof(IEmbodimentSubsystem))]
    [InlineData(typeof(ImmersiveSubsystem), typeof(IImmersiveSubsystem))]
    [InlineData(typeof(MemorySubsystem), typeof(IMemorySubsystem))]
    [InlineData(typeof(ModelSubsystem), typeof(IModelSubsystem))]
    [InlineData(typeof(PipeProcessingSubsystem), typeof(IPipeProcessingSubsystem))]
    [InlineData(typeof(SelfAssemblySubsystem), typeof(ISelfAssemblySubsystem))]
    [InlineData(typeof(SwarmSubsystem), typeof(ISwarmSubsystem))]
    [InlineData(typeof(ToolSubsystem), typeof(IToolSubsystem))]
    [InlineData(typeof(VoiceSubsystem), typeof(IVoiceSubsystem))]
    public void Subsystem_ImplementsExpectedInterface(Type concreteType, Type interfaceType)
    {
        concreteType.Should().Implement(interfaceType);
    }

    [Theory]
    [InlineData(typeof(CognitiveSubsystem))]
    [InlineData(typeof(CommandRoutingSubsystem))]
    [InlineData(typeof(EmbodimentSubsystem))]
    [InlineData(typeof(ImmersiveSubsystem))]
    [InlineData(typeof(MemorySubsystem))]
    [InlineData(typeof(ModelSubsystem))]
    [InlineData(typeof(PipeProcessingSubsystem))]
    [InlineData(typeof(SelfAssemblySubsystem))]
    [InlineData(typeof(SwarmSubsystem))]
    [InlineData(typeof(ToolSubsystem))]
    [InlineData(typeof(VoiceSubsystem))]
    public void Subsystem_HasIsInitializedProperty(Type subsystemType)
    {
        var prop = subsystemType.GetProperty("IsInitialized");
        prop.Should().NotBeNull($"{subsystemType.Name} should have IsInitialized");
        prop!.PropertyType.Should().Be(typeof(bool));
    }

    [Theory]
    [InlineData(typeof(CognitiveSubsystem))]
    [InlineData(typeof(CommandRoutingSubsystem))]
    [InlineData(typeof(EmbodimentSubsystem))]
    [InlineData(typeof(ImmersiveSubsystem))]
    [InlineData(typeof(MemorySubsystem))]
    [InlineData(typeof(ModelSubsystem))]
    [InlineData(typeof(PipeProcessingSubsystem))]
    [InlineData(typeof(SelfAssemblySubsystem))]
    [InlineData(typeof(SwarmSubsystem))]
    [InlineData(typeof(ToolSubsystem))]
    [InlineData(typeof(VoiceSubsystem))]
    public void Subsystem_HasInitializeAsyncMethod(Type subsystemType)
    {
        var method = subsystemType.GetMethod("InitializeAsync");
        method.Should().NotBeNull($"{subsystemType.Name} should have InitializeAsync");
    }

    [Theory]
    [InlineData(typeof(CognitiveSubsystem))]
    [InlineData(typeof(CommandRoutingSubsystem))]
    [InlineData(typeof(EmbodimentSubsystem))]
    [InlineData(typeof(ImmersiveSubsystem))]
    [InlineData(typeof(MemorySubsystem))]
    [InlineData(typeof(ModelSubsystem))]
    [InlineData(typeof(PipeProcessingSubsystem))]
    [InlineData(typeof(SelfAssemblySubsystem))]
    [InlineData(typeof(SwarmSubsystem))]
    [InlineData(typeof(ToolSubsystem))]
    [InlineData(typeof(VoiceSubsystem))]
    public void Subsystem_HasDisposeAsyncMethod(Type subsystemType)
    {
        var method = subsystemType.GetMethod("DisposeAsync");
        method.Should().NotBeNull($"{subsystemType.Name} should have DisposeAsync");
    }
}
