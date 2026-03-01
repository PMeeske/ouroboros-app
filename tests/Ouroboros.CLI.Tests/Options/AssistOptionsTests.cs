using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class AssistOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new AssistOptions();

        options.Voice.Should().BeFalse();
        options.DslMode.Should().BeFalse();
        options.Persona.Should().Be("Iaret");
        options.EmbedModel.Should().Be("nomic-embed-text");
        options.QdrantEndpoint.Should().Be("http://localhost:6334");
        options.Mode.Should().Be("suggest");
        options.Culture.Should().BeNull();
        options.Dsl.Should().BeNull();
        options.Goal.Should().BeNull();
        options.PartialToken.Should().BeNull();
        options.MaxSuggestions.Should().Be(5);
        options.Interactive.Should().BeFalse();
        options.Stream.Should().BeFalse();
        options.VoiceOnly.Should().BeFalse();
        options.LocalTts.Should().BeTrue();
        options.VoiceChannel.Should().BeFalse();
        options.VoiceLoop.Should().BeFalse();
    }

    [Fact]
    public void ImplementsIVoiceOptions()
    {
        var options = new AssistOptions();
        options.Should().BeAssignableTo<IVoiceOptions>();
    }

    [Fact]
    public void InheritsFromBaseModelOptions()
    {
        var options = new AssistOptions();
        options.Should().BeAssignableTo<BaseModelOptions>();
    }

    [Fact]
    public void IVoiceOptions_Endpoint_FallbackToDefault()
    {
        IVoiceOptions options = new AssistOptions();
        options.Endpoint.Should().Be("http://localhost:11434");
    }
}
