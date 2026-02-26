using FluentAssertions;
using Ouroboros.Application;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class PipelineTokenAttributeTests
{
    [Fact]
    public void Constructor_WithNames_ShouldSetNames()
    {
        var attr = new PipelineTokenAttribute("Draft", "draft", "d");

        attr.Names.Should().HaveCount(3);
        attr.Names[0].Should().Be("Draft");
        attr.Names[1].Should().Be("draft");
        attr.Names[2].Should().Be("d");
    }

    [Fact]
    public void Constructor_NoNames_ShouldBeEmpty()
    {
        var attr = new PipelineTokenAttribute();

        attr.Names.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_NullNames_ShouldBeEmpty()
    {
        var attr = new PipelineTokenAttribute(null!);

        attr.Names.Should().BeEmpty();
    }
}
