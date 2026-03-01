using FluentAssertions;
using Xunit;
using MemoryTrace = Ouroboros.Application.Personality.MemoryTrace;

namespace Ouroboros.Tests.Personality.Consciousness;

[Trait("Category", "Unit")]
public class MemoryTraceTests
{
    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var trace = MemoryTrace.Create("test content");

        trace.Content.Should().Be("test content");
        trace.EncodingStrength.Should().Be(0.6);
        trace.ConsolidationLevel.Should().Be(0.1);
        trace.IsConsolidated.Should().BeFalse();
        trace.LastRetrieved.Should().BeNull();
        trace.RetrievalCount.Should().Be(0);
    }

    [Fact]
    public void Create_CustomStrength_ShouldSet()
    {
        var trace = MemoryTrace.Create("test", 0.9);

        trace.EncodingStrength.Should().Be(0.9);
    }

    [Fact]
    public void Consolidate_ShouldIncreaseConsolidationLevel()
    {
        var trace = MemoryTrace.Create("test");

        var consolidated = trace.Consolidate();

        consolidated.ConsolidationLevel.Should().BeGreaterThan(trace.ConsolidationLevel);
    }

    [Fact]
    public void Consolidate_MultipleTimse_ShouldReachConsolidated()
    {
        var trace = MemoryTrace.Create("test");

        // Consolidate multiple times (each adds 0.2, start at 0.1)
        trace = trace.Consolidate(); // 0.3
        trace = trace.Consolidate(); // 0.5
        trace = trace.Consolidate(); // 0.7 (not > 0.7 yet)
        trace = trace.Consolidate(); // 0.9 (> 0.7, so IsConsolidated)

        trace.IsConsolidated.Should().BeTrue();
    }

    [Fact]
    public void Consolidate_ShouldNotExceedMax()
    {
        var trace = MemoryTrace.Create("test");

        for (int i = 0; i < 10; i++)
            trace = trace.Consolidate();

        trace.ConsolidationLevel.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void Retrieve_ShouldIncrementCount()
    {
        var trace = MemoryTrace.Create("test");

        var retrieved = trace.Retrieve();

        retrieved.RetrievalCount.Should().Be(1);
        retrieved.LastRetrieved.Should().NotBeNull();
    }

    [Fact]
    public void Retrieve_ShouldStrengthMemory()
    {
        var trace = MemoryTrace.Create("test");

        var retrieved = trace.Retrieve();

        retrieved.EncodingStrength.Should().BeGreaterThan(trace.EncodingStrength);
    }

    [Fact]
    public void Retrieve_MultipleRetrievals_ShouldHaveDiminishingReturns()
    {
        var trace = MemoryTrace.Create("test", 0.5);

        var first = trace.Retrieve();
        var second = first.Retrieve();

        var firstBoost = first.EncodingStrength - trace.EncodingStrength;
        var secondBoost = second.EncodingStrength - first.EncodingStrength;

        secondBoost.Should().BeLessThan(firstBoost);
    }
}
