using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Models;

[Trait("Category", "Unit")]
public class CuriosityDriverTests
{
    [Fact]
    public void CanAskAgain_RecentlyAsked_ShouldReturnFalse()
    {
        var driver = new CuriosityDriver("math", 0.8,
            new[] { "What is calculus?" }, DateTime.UtcNow, 1);

        driver.CanAskAgain(TimeSpan.FromMinutes(5)).Should().BeFalse();
    }

    [Fact]
    public void CanAskAgain_LongAgo_ShouldReturnTrue()
    {
        var driver = new CuriosityDriver("math", 0.8,
            new[] { "What is calculus?" }, DateTime.UtcNow.AddHours(-1), 1);

        driver.CanAskAgain(TimeSpan.FromMinutes(5)).Should().BeTrue();
    }
}
