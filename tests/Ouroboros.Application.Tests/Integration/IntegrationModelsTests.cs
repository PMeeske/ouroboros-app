using FluentAssertions;
using Ouroboros.Application.Integration;
using Xunit;

namespace Ouroboros.Tests.Integration;

[Trait("Category", "Unit")]
public class IntegrationModelsTests
{
    // --- CognitiveState ---

    [Fact]
    public void CognitiveState_ShouldSetProperties()
    {
        var now = DateTime.UtcNow;
        var actions = new List<string> { "perceive", "reason" };
        var state = new CognitiveState(true, 5, now, "reasoning", actions);

        state.IsRunning.Should().BeTrue();
        state.CyclesCompleted.Should().Be(5);
        state.LastCycleTime.Should().Be(now);
        state.CurrentPhase.Should().Be("reasoning");
        state.RecentActions.Should().HaveCount(2);
    }

    // --- CycleOutcome ---

    [Fact]
    public void CycleOutcome_ShouldSetProperties()
    {
        var metrics = new Dictionary<string, object> { ["latency"] = 100 };
        var outcome = new CycleOutcome(
            true, "perception", TimeSpan.FromMilliseconds(50),
            new List<string> { "observe" }, metrics);

        outcome.Success.Should().BeTrue();
        outcome.Phase.Should().Be("perception");
        outcome.Duration.Should().Be(TimeSpan.FromMilliseconds(50));
        outcome.ActionsPerformed.Should().HaveCount(1);
        outcome.Metrics.Should().ContainKey("latency");
    }

    // --- MetacognitiveInsights ---

    [Fact]
    public void MetacognitiveInsights_ShouldSetProperties()
    {
        var attention = new Dictionary<string, int> { ["perception"] = 5, ["reasoning"] = 3 };
        var insights = new MetacognitiveInsights(
            new List<string> { "conflict1" },
            new List<string> { "pattern1", "pattern2" },
            new List<string> { "reflect on X" },
            0.85,
            attention);

        insights.DetectedConflicts.Should().HaveCount(1);
        insights.IdentifiedPatterns.Should().HaveCount(2);
        insights.ReflectionOpportunities.Should().HaveCount(1);
        insights.OverallCoherence.Should().Be(0.85);
        insights.AttentionDistribution.Should().HaveCount(2);
    }

    // --- LearningResult ---

    [Fact]
    public void LearningResult_ShouldSetProperties()
    {
        var result = new LearningResult(
            10, 3, 2, 0.15,
            new List<Insight>());

        result.EpisodesProcessed.Should().Be(10);
        result.RulesLearned.Should().Be(3);
        result.AdaptersUpdated.Should().Be(2);
        result.PerformanceImprovement.Should().Be(0.15);
        result.Insights.Should().BeEmpty();
    }

    // --- SystemEvent subclasses ---

    [Fact]
    public void ConsciousnessStateChangedEvent_ShouldSetProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var evt = new ConsciousnessStateChangedEvent(
            id, now, "consciousness",
            "Aware", new List<string> { "item1", "item2" });

        evt.EventId.Should().Be(id);
        evt.Timestamp.Should().Be(now);
        evt.Source.Should().Be("consciousness");
        evt.NewState.Should().Be("Aware");
        evt.ActiveItems.Should().HaveCount(2);
    }

    [Fact]
    public void GoalExecutedEvent_ShouldSetProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var evt = new GoalExecutedEvent(
            id, now, "planner",
            "Search for data", true, TimeSpan.FromSeconds(5));

        evt.EventId.Should().Be(id);
        evt.Source.Should().Be("planner");
        evt.Goal.Should().Be("Search for data");
        evt.Success.Should().BeTrue();
        evt.Duration.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void LearningCompletedEvent_ShouldSetProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var evt = new LearningCompletedEvent(
            id, now, "learner", 10, 3);

        evt.Source.Should().Be("learner");
        evt.EpisodesProcessed.Should().Be(10);
        evt.RulesLearned.Should().Be(3);
    }

    [Fact]
    public void ReasoningCompletedEvent_ShouldSetProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var evt = new ReasoningCompletedEvent(
            id, now, "reasoner",
            "What is X?", "X is Y", 0.95);

        evt.Source.Should().Be("reasoner");
        evt.Query.Should().Be("What is X?");
        evt.Answer.Should().Be("X is Y");
        evt.Confidence.Should().Be(0.95);
    }

    // --- LearningConfig ---

    [Fact]
    public void LearningConfig_Default_ShouldHaveExpectedValues()
    {
        var config = LearningConfig.Default;

        config.ConsolidateMemories.Should().BeTrue();
        config.UpdateAdapters.Should().BeTrue();
        config.ExtractRules.Should().BeTrue();
    }
}
