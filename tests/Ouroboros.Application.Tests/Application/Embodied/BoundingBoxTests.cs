using FluentAssertions;
using Ouroboros.Application.Embodied;
using Xunit;

namespace Ouroboros.Tests.Application.Embodied;

[Trait("Category", "Unit")]
public class BoundingBoxTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var box = new BoundingBox(10, 20, 100, 50);

        box.X.Should().Be(10);
        box.Y.Should().Be(20);
        box.Width.Should().Be(100);
        box.Height.Should().Be(50);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new BoundingBox(1, 2, 3, 4);
        var b = new BoundingBox(1, 2, 3, 4);

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_ShouldNotBeEqual()
    {
        var a = new BoundingBox(1, 2, 3, 4);
        var b = new BoundingBox(5, 6, 7, 8);

        a.Should().NotBe(b);
    }
}
