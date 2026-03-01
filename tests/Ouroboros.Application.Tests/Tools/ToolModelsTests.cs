using FluentAssertions;
using Ouroboros.Application.Tools;
using Xunit;

namespace Ouroboros.Tests.Tools;

[Trait("Category", "Unit")]
public class ToolModelsTests
{
    // --- ActionGene ---

    [Fact]
    public void ActionGene_ShouldSetProperties()
    {
        var gene = new ActionGene("search", "web_search", 0.85);

        gene.ActionType.Should().Be("search");
        gene.ActionName.Should().Be("web_search");
        gene.Priority.Should().Be(0.85);
    }

    [Fact]
    public void ActionGene_RecordEquality_ShouldWork()
    {
        var a = new ActionGene("search", "web", 1.0);
        var b = new ActionGene("search", "web", 1.0);

        a.Should().Be(b);
    }

    // --- ExecutionRecord ---

    [Fact]
    public void ExecutionRecord_ShouldSetAllProperties()
    {
        var metadata = new Dictionary<string, string> { ["key"] = "val" };
        var record = new ExecutionRecord(
            "r1", "tool", "my-tool", "input", "output",
            true, TimeSpan.FromSeconds(2), DateTime.UtcNow, metadata);

        record.Id.Should().Be("r1");
        record.ExecutionType.Should().Be("tool");
        record.Name.Should().Be("my-tool");
        record.Input.Should().Be("input");
        record.Output.Should().Be("output");
        record.Success.Should().BeTrue();
        record.Duration.Should().Be(TimeSpan.FromSeconds(2));
        record.Metadata.Should().ContainKey("key");
    }

    // --- LearningStats ---

    [Fact]
    public void LearningStats_ShouldSetAllProperties()
    {
        var stats = new LearningStats(100, 50, 25, 80, 10, 200, 500);

        stats.TotalToolExecutions.Should().Be(100);
        stats.TotalSkillExecutions.Should().Be(50);
        stats.TotalPipelineExecutions.Should().Be(25);
        stats.SuccessfulExecutions.Should().Be(80);
        stats.LearnedPatterns.Should().Be(10);
        stats.ConceptGraphNodes.Should().Be(200);
        stats.ExecutionLogSize.Should().Be(500);
    }

    // --- LLMActionSuggestion ---

    [Fact]
    public void LLMActionSuggestion_ShouldSetProperties()
    {
        var suggestion = new LLMActionSuggestion("search", "web_search", "User asked for info");

        suggestion.ActionType.Should().Be("search");
        suggestion.ActionName.Should().Be("web_search");
        suggestion.Reasoning.Should().Be("User asked for info");
    }

    // --- PatternSummary ---

    [Fact]
    public void PatternSummary_ShouldSetProperties()
    {
        var summary = new PatternSummary("tool", "Search web", new List<string> { "search", "parse" }, 0.9);

        summary.PatternType.Should().Be("tool");
        summary.GoalDescription.Should().Be("Search web");
        summary.Actions.Should().HaveCount(2);
        summary.SuccessRate.Should().Be(0.9);
    }

    // --- ToolConfiguration ---

    [Fact]
    public void ToolConfiguration_ShouldSetProperties()
    {
        var config = new ToolConfiguration("search", "Web search", "google", 10.0, 2, true, "extra");

        config.Name.Should().Be("search");
        config.Description.Should().Be("Web search");
        config.SearchProvider.Should().Be("google");
        config.TimeoutSeconds.Should().Be(10.0);
        config.MaxRetries.Should().Be(2);
        config.CacheResults.Should().BeTrue();
        config.CustomParameters.Should().Be("extra");
    }

    [Fact]
    public void ToolConfiguration_Default_ShouldHaveExpectedValues()
    {
        var config = ToolConfiguration.Default("my-tool", "My tool");

        config.Name.Should().Be("my-tool");
        config.Description.Should().Be("My tool");
        config.SearchProvider.Should().BeNull();
        config.TimeoutSeconds.Should().Be(30.0);
        config.MaxRetries.Should().Be(3);
        config.CacheResults.Should().BeTrue();
        config.CustomParameters.Should().BeNull();
    }

    // --- ToolConfigurationGene ---

    [Fact]
    public void ToolConfigurationGene_ShouldSetProperties()
    {
        var gene = new ToolConfigurationGene("timeout", "30");

        gene.Key.Should().Be("timeout");
        gene.Value.Should().Be("30");
    }

    [Fact]
    public void ToolConfigurationGene_NullValue_ShouldBeAllowed()
    {
        var gene = new ToolConfigurationGene("flag", null);

        gene.Value.Should().BeNull();
    }

    // --- InterconnectedPattern ---

    [Fact]
    public void InterconnectedPattern_ShouldSetProperties()
    {
        var pattern = new InterconnectedPattern(
            "p1", "composite", "Find and process",
            new List<string> { "search", "parse" },
            new List<string> { "web-skill" },
            0.85, 10, DateTime.UtcNow, DateTime.UtcNow,
            new float[] { 0.1f, 0.2f, 0.3f });

        pattern.Id.Should().Be("p1");
        pattern.PatternType.Should().Be("composite");
        pattern.GoalDescription.Should().Be("Find and process");
        pattern.ToolSequence.Should().HaveCount(2);
        pattern.SkillSequence.Should().HaveCount(1);
        pattern.SuccessRate.Should().Be(0.85);
        pattern.UsageCount.Should().Be(10);
        pattern.EmbeddingVector.Should().HaveCount(3);
    }

    // --- LearnedToolPattern ---

    [Fact]
    public void LearnedToolPattern_ShouldSetProperties()
    {
        var config = ToolConfiguration.Default("tool", "desc");
        var pattern = new LearnedToolPattern(
            "lp1", "search goal", "web_search", config,
            0.9, 5, DateTime.UtcNow, DateTime.UtcNow,
            new List<string> { "related-goal" });

        pattern.Id.Should().Be("lp1");
        pattern.Goal.Should().Be("search goal");
        pattern.ToolName.Should().Be("web_search");
        pattern.Configuration.Should().Be(config);
        pattern.SuccessRate.Should().Be(0.9);
        pattern.UsageCount.Should().Be(5);
        pattern.RelatedGoals.Should().HaveCount(1);
    }

    // --- SmartSuggestion ---

    [Fact]
    public void SmartSuggestion_ShouldHaveDefaults()
    {
        var suggestion = new SmartSuggestion();

        suggestion.Goal.Should().BeEmpty();
        suggestion.MeTTaSuggestions.Should().BeEmpty();
        suggestion.SimilarPatterns.Should().BeEmpty();
        suggestion.LLMSuggestion.Should().BeNull();
        suggestion.EvolvedSequence.Should().BeEmpty();
        suggestion.RelatedConcepts.Should().BeEmpty();
    }

    [Fact]
    public void SmartSuggestion_ShouldSetProperties()
    {
        var suggestion = new SmartSuggestion
        {
            Goal = "Search web",
            Timestamp = DateTime.UtcNow,
            ConfidenceScore = 0.95,
            MeTTaSuggestions = new List<string> { "suggestion1" },
            RelatedConcepts = new List<string> { "concept1", "concept2" }
        };

        suggestion.Goal.Should().Be("Search web");
        suggestion.ConfidenceScore.Should().Be(0.95);
        suggestion.MeTTaSuggestions.Should().HaveCount(1);
        suggestion.RelatedConcepts.Should().HaveCount(2);
    }
}
