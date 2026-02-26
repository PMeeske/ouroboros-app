using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality;

[Trait("Category", "Unit")]
public class CompositeThoughtTests
{
    [Fact]
    public void Constructor_ShouldSetRequiredProperties()
    {
        var id = Guid.NewGuid();
        var thought = new CompositeThought
        {
            Id = id,
            SourceThoughts = new[] { "A", "B" },
            Relationship = "synthesis",
            CompositeVector = new float[] { 0.1f, 0.2f },
            Dimension = 2,
            CreatedAt = DateTime.UtcNow
        };

        thought.Id.Should().Be(id);
        thought.SourceThoughts.Should().HaveCount(2);
        thought.Relationship.Should().Be("synthesis");
        thought.CompositeVector.Should().HaveCount(2);
        thought.Dimension.Should().Be(2);
        thought.Metadata.Should().BeNull();
    }

    [Fact]
    public void Metadata_ShouldBeOptional()
    {
        var thought = new CompositeThought
        {
            Id = Guid.NewGuid(),
            SourceThoughts = new[] { "A" },
            Relationship = "test",
            CompositeVector = new float[] { 1f },
            Dimension = 1,
            CreatedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object> { ["key"] = "value" }
        };

        thought.Metadata.Should().ContainKey("key");
    }
}
