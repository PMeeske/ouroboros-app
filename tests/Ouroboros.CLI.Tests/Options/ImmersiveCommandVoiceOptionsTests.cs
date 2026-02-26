using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class ImmersiveCommandVoiceOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new ImmersiveCommandVoiceOptions();

        options.Persona.Should().Be("Iaret");
        options.Model.Should().Be("deepseek-v3.1:671b-cloud");
        options.Endpoint.Should().Be("http://localhost:11434");
        options.EmbedModel.Should().Be("nomic-embed-text");
        options.QdrantEndpoint.Should().Be("http://localhost:6334");
    }

    [Fact]
    public void ImplementsIVoiceOptions()
    {
        var options = new ImmersiveCommandVoiceOptions();
        options.Should().BeAssignableTo<IVoiceOptions>();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var options = new ImmersiveCommandVoiceOptions
        {
            Persona = "Test",
            Model = "gpt-4",
            Endpoint = "https://api.openai.com",
            EmbedModel = "embed-v3",
            QdrantEndpoint = "http://qdrant:6334"
        };

        options.Persona.Should().Be("Test");
        options.Model.Should().Be("gpt-4");
        options.Endpoint.Should().Be("https://api.openai.com");
    }
}
