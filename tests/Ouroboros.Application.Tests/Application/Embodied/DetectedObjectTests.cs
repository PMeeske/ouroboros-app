using FluentAssertions;
using Ouroboros.Application.Embodied;
using Xunit;

namespace Ouroboros.Tests.Application.Embodied;

[Trait("Category", "Unit")]
public class DetectedObjectTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var box = new BoundingBox(0, 0, 100, 100);
        var obj = new DetectedObject("car", 0.95f, box);

        obj.Label.Should().Be("car");
        obj.Confidence.Should().Be(0.95f);
        obj.BoundingBox.Should().Be(box);
    }
}
