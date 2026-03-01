using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class IVoiceOptionsTests
{
    [Fact]
    public void IVoiceOptions_IsInterface()
    {
        typeof(IVoiceOptions).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IVoiceOptions_HasVoiceProperty()
    {
        typeof(IVoiceOptions).GetProperty("Voice").Should().NotBeNull();
    }

    [Fact]
    public void IVoiceOptions_HasPersonaProperty()
    {
        typeof(IVoiceOptions).GetProperty("Persona").Should().NotBeNull();
    }

    [Fact]
    public void IVoiceOptions_HasModelProperty()
    {
        typeof(IVoiceOptions).GetProperty("Model").Should().NotBeNull();
    }

    [Fact]
    public void IVoiceOptions_HasEndpointProperty()
    {
        typeof(IVoiceOptions).GetProperty("Endpoint").Should().NotBeNull();
    }

    [Fact]
    public void IVoiceOptions_HasEmbedModelProperty()
    {
        typeof(IVoiceOptions).GetProperty("EmbedModel").Should().NotBeNull();
    }

    [Fact]
    public void IVoiceOptions_HasQdrantEndpointProperty()
    {
        typeof(IVoiceOptions).GetProperty("QdrantEndpoint").Should().NotBeNull();
    }

    [Fact]
    public void IVoiceOptions_HasVoiceOnlyProperty()
    {
        typeof(IVoiceOptions).GetProperty("VoiceOnly").Should().NotBeNull();
    }

    [Fact]
    public void IVoiceOptions_HasLocalTtsProperty()
    {
        typeof(IVoiceOptions).GetProperty("LocalTts").Should().NotBeNull();
    }

    [Fact]
    public void IVoiceOptions_HasVoiceLoopProperty()
    {
        typeof(IVoiceOptions).GetProperty("VoiceLoop").Should().NotBeNull();
    }

    [Theory]
    [InlineData(typeof(OrchestratorOptions))]
    [InlineData(typeof(PipelineOptions))]
    public void ConcreteType_ImplementsIVoiceOptions(Type concreteType)
    {
        concreteType.Should().Implement<IVoiceOptions>();
    }
}
