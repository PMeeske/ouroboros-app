using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

// Concrete subclass for testing abstract BaseModelOptions
internal class TestModelOptions : BaseModelOptions { }

[Trait("Category", "Unit")]
public class BaseModelOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new TestModelOptions();

        options.Model.Should().Be("deepseek-v3.1:671b-cloud");
        options.Embed.Should().Be("nomic-embed-text");
        options.Temperature.Should().Be(0.7);
        options.MaxTokens.Should().Be(2000);
        options.TimeoutSeconds.Should().Be(120);
        options.Debug.Should().BeFalse();
        options.Endpoint.Should().BeNull();
        options.ApiKey.Should().BeNull();
        options.EndpointType.Should().Be("auto");
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var options = new TestModelOptions
        {
            Model = "gpt-4",
            Embed = "text-embedding-3-small",
            Temperature = 0.5,
            MaxTokens = 4096,
            TimeoutSeconds = 30,
            Debug = true,
            Endpoint = "https://api.openai.com",
            ApiKey = "test-key",
            EndpointType = "openai"
        };

        options.Model.Should().Be("gpt-4");
        options.Embed.Should().Be("text-embedding-3-small");
        options.Temperature.Should().Be(0.5);
        options.MaxTokens.Should().Be(4096);
        options.TimeoutSeconds.Should().Be(30);
        options.Debug.Should().BeTrue();
        options.Endpoint.Should().Be("https://api.openai.com");
        options.ApiKey.Should().Be("test-key");
        options.EndpointType.Should().Be("openai");
    }
}
