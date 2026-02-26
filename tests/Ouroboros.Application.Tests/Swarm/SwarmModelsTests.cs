using FluentAssertions;
using Ouroboros.Application.Swarm;
using Xunit;

namespace Ouroboros.Tests.Swarm;

[Trait("Category", "Unit")]
public class SwarmModelsTests
{
    [Fact]
    public void SwarmInitResult_ShouldSetProperties()
    {
        var result = new SwarmInitResult(true, "swarm-1", "mesh", 10, "Initialized");

        result.Success.Should().BeTrue();
        result.SwarmId.Should().Be("swarm-1");
        result.Topology.Should().Be("mesh");
        result.MaxAgents.Should().Be(10);
        result.Message.Should().Be("Initialized");
    }

    [Fact]
    public void SwarmStatusResult_ShouldSetProperties()
    {
        var result = new SwarmStatusResult(true, "swarm-1", 5, "hierarchical", "{}");

        result.Active.Should().BeTrue();
        result.SwarmId.Should().Be("swarm-1");
        result.AgentCount.Should().Be(5);
        result.Topology.Should().Be("hierarchical");
        result.RawJson.Should().Be("{}");
    }

    [Fact]
    public void SwarmHealthResult_ShouldSetProperties()
    {
        var result = new SwarmHealthResult(true, "running", "All agents healthy");

        result.Healthy.Should().BeTrue();
        result.Status.Should().Be("running");
        result.Details.Should().Be("All agents healthy");
    }

    [Fact]
    public void AgentSpawnResult_ShouldSetProperties()
    {
        var result = new AgentSpawnResult(true, "agent-1", "coder", "my-coder", "Spawned");

        result.Success.Should().BeTrue();
        result.AgentId.Should().Be("agent-1");
        result.AgentType.Should().Be("coder");
        result.Name.Should().Be("my-coder");
        result.Message.Should().Be("Spawned");
    }

    [Fact]
    public void AgentSpawnResult_NullName_ShouldBeAllowed()
    {
        var result = new AgentSpawnResult(true, "agent-1", "coder", null, "Spawned");

        result.Name.Should().BeNull();
    }

    [Fact]
    public void AgentStatusResult_ShouldSetProperties()
    {
        var result = new AgentStatusResult("agent-1", "active", "coder", "Working on task");

        result.AgentId.Should().Be("agent-1");
        result.Status.Should().Be("active");
        result.Type.Should().Be("coder");
        result.Details.Should().Be("Working on task");
    }

    [Fact]
    public void AgentListEntry_ShouldSetProperties()
    {
        var entry = new AgentListEntry("agent-1", "coder", "active", "my-coder");

        entry.AgentId.Should().Be("agent-1");
        entry.Type.Should().Be("coder");
        entry.Status.Should().Be("active");
        entry.Name.Should().Be("my-coder");
    }

    [Fact]
    public void TaskOrchestrationResult_ShouldSetProperties()
    {
        var result = new TaskOrchestrationResult(true, "task-1", "completed", "Done");

        result.Success.Should().BeTrue();
        result.TaskId.Should().Be("task-1");
        result.Status.Should().Be("completed");
        result.Result.Should().Be("Done");
    }

    [Fact]
    public void MemorySearchResult_ShouldSetProperties()
    {
        var result = new MemorySearchResult("auth-pattern", "JWT tokens", 0.95);

        result.Key.Should().Be("auth-pattern");
        result.Value.Should().Be("JWT tokens");
        result.Score.Should().Be(0.95);
    }

    [Fact]
    public void ClaudeFlowConfig_ShouldHaveDefaults()
    {
        var config = new ClaudeFlowConfig();

        config.Command.Should().Be("npx");
        config.Args.Should().NotBeEmpty();
        config.Topology.Should().Be("hierarchical-mesh");
        config.MaxAgents.Should().Be(15);
        config.Strategy.Should().Be("specialized");
        config.MemoryBackend.Should().Be("hybrid");
    }

    [Fact]
    public void ClaudeFlowConfig_ShouldAllowCustomValues()
    {
        var config = new ClaudeFlowConfig
        {
            Command = "claude-flow",
            Topology = "ring",
            MaxAgents = 8,
            Strategy = "balanced",
            MemoryBackend = "memory"
        };

        config.Command.Should().Be("claude-flow");
        config.Topology.Should().Be("ring");
        config.MaxAgents.Should().Be(8);
        config.Strategy.Should().Be("balanced");
        config.MemoryBackend.Should().Be("memory");
    }
}
