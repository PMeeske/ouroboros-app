using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Consciousness;

[Trait("Category", "Unit")]
public class AttentionalGateTests
{
    [Fact]
    public void Default_ShouldHaveExpectedValues()
    {
        var gate = AttentionalGate.Default();

        gate.Threshold.Should().Be(0.3);
        gate.Capacity.Should().Be(1.0);
        gate.PrimedCategories.Should().Contain("social");
        gate.FatigueFactor.Should().Be(0.001);
    }

    [Fact]
    public void Allows_HighSalience_ShouldPass()
    {
        var gate = AttentionalGate.Default();
        var stimulus = Stimulus.CreateUnconditioned("danger", new[] { "error" });

        gate.Allows(stimulus).Should().BeTrue();
    }

    [Fact]
    public void Allows_LowSalience_ShouldFail()
    {
        var gate = new AttentionalGate(0.9, 0.1, Array.Empty<string>(), 0.001, DateTime.UtcNow);
        var stimulus = Stimulus.CreateNeutral("boring", new[] { "meh" });

        gate.Allows(stimulus).Should().BeFalse();
    }

    [Fact]
    public void Allows_PrimedCategory_ShouldBoost()
    {
        var gate = new AttentionalGate(0.6, 1.0, new[] { "social" }, 0.001, DateTime.UtcNow);
        var stimulus = Stimulus.CreateNeutral("hello", new[] { "hi" }, "social");

        // Salience 0.5 * 1.5 = 0.75, threshold ~0.42 (0.6 * 0.7) -> passes
        gate.Allows(stimulus).Should().BeTrue();
    }

    [Fact]
    public void ApplyFatigue_ShouldReduceCapacity()
    {
        var gate = AttentionalGate.Default();

        var fatigued = gate.ApplyFatigue(TimeSpan.FromMinutes(60));

        fatigued.Capacity.Should().BeLessThan(gate.Capacity);
    }

    [Fact]
    public void ApplyFatigue_ShouldNotGoBelowMinimum()
    {
        var gate = AttentionalGate.Default();

        var fatigued = gate.ApplyFatigue(TimeSpan.FromHours(1000));

        fatigued.Capacity.Should().BeGreaterThanOrEqualTo(0.1);
    }

    [Fact]
    public void Reset_ShouldRestoreCapacity()
    {
        var gate = AttentionalGate.Default().ApplyFatigue(TimeSpan.FromMinutes(60));

        var reset = gate.Reset();

        reset.Capacity.Should().Be(1.0);
    }
}
