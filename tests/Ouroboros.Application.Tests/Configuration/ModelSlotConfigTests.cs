using FluentAssertions;
using Ouroboros.Application.Configuration;
using Xunit;

namespace Ouroboros.Tests.Configuration;

[Trait("Category", "Unit")]
public class ModelSlotConfigTests
{
    [Fact]
    public void Defaults_ShouldHaveExpectedValues()
    {
        var config = new ModelSlotConfig();

        config.Role.Should().BeEmpty();
        config.ModelName.Should().BeEmpty();
        config.ProviderType.Should().Be("ollama");
        config.Endpoint.Should().BeNull();
        config.ApiKey.Should().BeNull();
        config.ApiKeyEnvVar.Should().BeNull();
        config.Temperature.Should().BeNull();
        config.MaxTokens.Should().BeNull();
        config.Tags.Should().BeEmpty();
        config.AvgLatencyMs.Should().Be(1000);
    }

    [Fact]
    public void WithInit_ShouldSetProperties()
    {
        var config = new ModelSlotConfig
        {
            Role = "coder",
            ModelName = "gpt-4",
            ProviderType = "openai",
            Temperature = 0.2,
            MaxTokens = 4096,
            Tags = new[] { "code", "programming" }
        };

        config.Role.Should().Be("coder");
        config.ModelName.Should().Be("gpt-4");
        config.ProviderType.Should().Be("openai");
        config.Temperature.Should().Be(0.2);
        config.MaxTokens.Should().Be(4096);
        config.Tags.Should().HaveCount(2);
    }
}
