using FluentAssertions;
using Ouroboros.Application.Avatar;
using Xunit;

namespace Ouroboros.Tests.Avatar;

[Trait("Category", "Unit")]
public class AvatarModelsTests
{
    [Fact]
    public void AvatarVisualState_ShouldHaveExpectedValues()
    {
        Enum.GetValues<AvatarVisualState>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(AvatarVisualState.Idle)]
    [InlineData(AvatarVisualState.Listening)]
    [InlineData(AvatarVisualState.Thinking)]
    [InlineData(AvatarVisualState.Speaking)]
    [InlineData(AvatarVisualState.Encouraging)]
    public void AvatarVisualState_AllValues_ShouldBeDefined(AvatarVisualState state)
    {
        Enum.IsDefined(state).Should().BeTrue();
    }

    [Fact]
    public void AvatarStateSnapshot_Default_ShouldCreateIdleState()
    {
        var snapshot = AvatarStateSnapshot.Default();

        snapshot.VisualState.Should().Be(AvatarVisualState.Idle);
        snapshot.Mood.Should().Be("neutral");
        snapshot.Energy.Should().Be(0.5);
        snapshot.Positivity.Should().Be(0.5);
        snapshot.StatusText.Should().BeNull();
        snapshot.PersonaName.Should().Be("Iaret");
    }

    [Fact]
    public void AvatarStateSnapshot_Default_WithCustomPersona_ShouldWork()
    {
        var snapshot = AvatarStateSnapshot.Default("TestPersona");

        snapshot.PersonaName.Should().Be("TestPersona");
    }

    [Fact]
    public void AvatarStateSnapshot_Constructor_ShouldSetAllProperties()
    {
        var snapshot = new AvatarStateSnapshot(
            AvatarVisualState.Speaking, "happy", 0.8, 0.9, "Processing", "Iaret", DateTime.UtcNow);

        snapshot.VisualState.Should().Be(AvatarVisualState.Speaking);
        snapshot.Mood.Should().Be("happy");
        snapshot.Energy.Should().Be(0.8);
        snapshot.Positivity.Should().Be(0.9);
        snapshot.StatusText.Should().Be("Processing");
    }

    [Fact]
    public void AvatarStateSnapshot_Topic_ShouldBeSettableViaInit()
    {
        var snapshot = new AvatarStateSnapshot(
            AvatarVisualState.Idle, "neutral", 0.5, 0.5, null, "Iaret", DateTime.UtcNow)
        {
            Topic = "technical"
        };

        snapshot.Topic.Should().Be("technical");
    }
}
