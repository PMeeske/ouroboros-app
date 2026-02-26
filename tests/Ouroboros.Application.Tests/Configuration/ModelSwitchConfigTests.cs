using FluentAssertions;
using Ouroboros.Application.Configuration;
using Xunit;

namespace Ouroboros.Tests.Configuration;

[Trait("Category", "Unit")]
public class ModelSwitchConfigTests
{
    [Fact]
    public void Defaults_ShouldHaveExpectedValues()
    {
        var config = new ModelSwitchConfig();

        config.ChatModel.Should().BeNull();
        config.EmbeddingModel.Should().BeNull();
        config.ForceRemote.Should().BeFalse();
    }

    [Fact]
    public void WithInit_ShouldSetProperties()
    {
        var config = new ModelSwitchConfig
        {
            ChatModel = "gpt-4",
            EmbeddingModel = "nomic-embed",
            ForceRemote = true
        };

        config.ChatModel.Should().Be("gpt-4");
        config.EmbeddingModel.Should().Be("nomic-embed");
        config.ForceRemote.Should().BeTrue();
    }
}
