using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Consciousness;

[Trait("Category", "Unit")]
public class SecondOrderConditioningTests
{
    [Fact]
    public void Create_ShouldCalculateChainStrength()
    {
        var stimulus1 = Stimulus.CreateNeutral("s1", new[] { "a" });
        var response1 = Response.CreateEmotional("r1", "positive");
        var primary = ConditionedAssociation.Create(stimulus1, response1, 0.8);

        var stimulus2 = Stimulus.CreateNeutral("s2", new[] { "b" });
        var response2 = Response.CreateEmotional("r2", "happy");
        var secondary = ConditionedAssociation.Create(stimulus2, response2, 0.5);

        var chain = SecondOrderConditioning.Create(primary, secondary);

        chain.ChainStrength.Should().Be(0.8 * 0.5);
        chain.ChainDepth.Should().Be(2);
        chain.PrimaryAssociation.Should().BeSameAs(primary);
        chain.SecondaryAssociation.Should().BeSameAs(secondary);
    }
}
