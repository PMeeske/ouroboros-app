using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Consciousness;

[Trait("Category", "Unit")]
public class ConditionedAssociationTests
{
    private static ConditionedAssociation CreateTestAssociation(double initialStrength = 0.3)
    {
        var stimulus = Stimulus.CreateNeutral("greeting", new[] { "hello" });
        var response = Response.CreateEmotional("warmth", "warm");
        return ConditionedAssociation.Create(stimulus, response, initialStrength);
    }

    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var assoc = CreateTestAssociation();

        assoc.AssociationStrength.Should().Be(0.3);
        assoc.LearningRate.Should().Be(0.2);
        assoc.MaxStrength.Should().Be(1.0);
        assoc.ReinforcementCount.Should().Be(1);
        assoc.ExtinctionTrials.Should().Be(0);
        assoc.IsExtinct.Should().BeFalse();
    }

    [Fact]
    public void Reinforce_ShouldIncreaseStrength()
    {
        var assoc = CreateTestAssociation(0.3);

        var reinforced = assoc.Reinforce();

        reinforced.AssociationStrength.Should().BeGreaterThan(0.3);
        reinforced.ReinforcementCount.Should().Be(2);
    }

    [Fact]
    public void Reinforce_ShouldResetExtinctionTrials()
    {
        var assoc = CreateTestAssociation().ApplyExtinction();

        var reinforced = assoc.Reinforce();

        reinforced.ExtinctionTrials.Should().Be(0);
        reinforced.IsExtinct.Should().BeFalse();
    }

    [Fact]
    public void ApplyExtinction_ShouldDecreaseStrength()
    {
        var assoc = CreateTestAssociation(0.5);

        var extinguished = assoc.ApplyExtinction();

        extinguished.AssociationStrength.Should().BeLessThan(0.5);
        extinguished.ExtinctionTrials.Should().Be(1);
    }

    [Fact]
    public void ApplyExtinction_MultipleTimes_ShouldBecomeExtinct()
    {
        var assoc = CreateTestAssociation(0.3);

        for (int i = 0; i < 30; i++)
            assoc = assoc.ApplyExtinction();

        assoc.IsExtinct.Should().BeTrue();
    }

    [Fact]
    public void ApplySpontaneousRecovery_NotExtinct_ShouldReturnSame()
    {
        var assoc = CreateTestAssociation(0.5);

        var result = assoc.ApplySpontaneousRecovery(TimeSpan.FromHours(24));

        result.AssociationStrength.Should().Be(0.5);
    }

    [Fact]
    public void StimulusId_ShouldReturnStimulusId()
    {
        var assoc = CreateTestAssociation();

        assoc.StimulusId.Should().Be(assoc.Stimulus.Id);
    }

    [Fact]
    public void ResponseId_ShouldReturnResponseId()
    {
        var assoc = CreateTestAssociation();

        assoc.ResponseId.Should().Be(assoc.Response.Id);
    }
}
