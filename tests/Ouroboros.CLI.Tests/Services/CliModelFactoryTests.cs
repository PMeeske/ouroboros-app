using LangChain.Providers.Ollama;
using Ouroboros.CLI.Services;

namespace Ouroboros.Tests.CLI.Services;

[Trait("Category", "Unit")]
public class CliModelFactoryTests
{
    [Fact]
    public void ApplyModelPreset_NullModelName_DoesNotThrow()
    {
        var model = new OllamaChatModel(new OllamaProvider(), "test");

        var action = () => CliModelFactory.ApplyModelPreset(model, null!);

        action.Should().NotThrow();
    }

    [Fact]
    public void ApplyModelPreset_EmptyModelName_DoesNotThrow()
    {
        var model = new OllamaChatModel(new OllamaProvider(), "test");

        var action = () => CliModelFactory.ApplyModelPreset(model, "");

        action.Should().NotThrow();
    }

    [Fact]
    public void ApplyModelPreset_UnknownModel_DoesNotThrow()
    {
        var model = new OllamaChatModel(new OllamaProvider(), "test");

        var action = () => CliModelFactory.ApplyModelPreset(model, "some-unknown-model");

        action.Should().NotThrow();
    }

    [Fact]
    public void ApplyModelPreset_WithRole_NullModelName_DoesNotThrow()
    {
        var model = new OllamaChatModel(new OllamaProvider(), "test");

        var action = () => CliModelFactory.ApplyModelPreset(model, null!, "summarize");

        action.Should().NotThrow();
    }

    [Fact]
    public void TryCreateEmbeddingModel_InvalidEndpoint_ReturnsNull()
    {
        var result = CliModelFactory.TryCreateEmbeddingModel(
            null, null, ChatEndpointType.Auto, "nomic-embed-text");

        // Without a valid endpoint/provider this should safely return null
        // (the factory swallows exceptions)
        result.Should().BeNull();
    }
}
