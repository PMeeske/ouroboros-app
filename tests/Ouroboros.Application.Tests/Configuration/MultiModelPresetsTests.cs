using FluentAssertions;
using Ouroboros.Application.Configuration;
using Xunit;

namespace Ouroboros.Tests.Configuration;

[Trait("Category", "Unit")]
public class MultiModelPresetsTests
{
    [Fact]
    public void All_ShouldContainPresets()
    {
        MultiModelPresets.All.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void AnthropicMasterOllamaSub_ShouldHaveExpectedName()
    {
        MultiModelPresets.AnthropicMasterOllamaSub.Name.Should().Be("anthropic-ollama");
    }

    [Fact]
    public void AnthropicMasterOllamaLite_ShouldHaveExpectedName()
    {
        MultiModelPresets.AnthropicMasterOllamaLite.Name.Should().Be("anthropic-ollama-lite");
    }

    [Fact]
    public void AnthropicMasterOllamaSub_ShouldHaveModels()
    {
        MultiModelPresets.AnthropicMasterOllamaSub.Models.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GetByName_ExistingPreset_ShouldReturn()
    {
        var result = MultiModelPresets.GetByName("anthropic-ollama");

        result.Should().NotBeNull();
        result!.Name.Should().Be("anthropic-ollama");
    }

    [Fact]
    public void GetByName_CaseInsensitive_ShouldReturn()
    {
        var result = MultiModelPresets.GetByName("ANTHROPIC-OLLAMA");

        result.Should().NotBeNull();
    }

    [Fact]
    public void GetByName_NonExistent_ShouldReturnNull()
    {
        var result = MultiModelPresets.GetByName("does-not-exist");

        result.Should().BeNull();
    }

    [Fact]
    public void ListNames_ShouldReturnAllNames()
    {
        var names = MultiModelPresets.ListNames().ToList();

        names.Should().Contain("anthropic-ollama");
        names.Should().Contain("anthropic-ollama-lite");
    }
}
