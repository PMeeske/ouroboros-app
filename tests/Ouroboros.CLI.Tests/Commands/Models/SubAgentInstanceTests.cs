using Moq;
using Ouroboros.Abstractions.Core;
using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Models;

[Trait("Category", "Unit")]
public class SubAgentInstanceTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var capabilities = new HashSet<string> { "code", "review" };
        var mockModel = new Mock<IChatCompletionModel>();

        var agent = new SubAgentInstance("agent-1", "Coder", capabilities, mockModel.Object);

        agent.AgentId.Should().Be("agent-1");
        agent.Name.Should().Be("Coder");
        agent.Capabilities.Should().BeEquivalentTo(capabilities);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithNoModel_ReturnsNoModelMessage()
    {
        var agent = new SubAgentInstance("agent-1", "Coder", new HashSet<string>(), null);

        var result = await agent.ExecuteTaskAsync("write code");

        result.Should().Contain("[Coder]");
        result.Should().Contain("No model available");
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithModel_CallsModelAndReturnsResult()
    {
        var mockModel = new Mock<IChatCompletionModel>();
        mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Generated code for task");
        var capabilities = new HashSet<string> { "coding" };

        var agent = new SubAgentInstance("agent-1", "Coder", capabilities, mockModel.Object);

        var result = await agent.ExecuteTaskAsync("write hello world");

        result.Should().Be("Generated code for task");
        mockModel.Verify(m => m.GenerateTextAsync(
            It.Is<string>(s => s.Contains("Coder") && s.Contains("coding") && s.Contains("write hello world")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithCancellationToken_PassesToModel()
    {
        var mockModel = new Mock<IChatCompletionModel>();
        using var cts = new CancellationTokenSource();
        mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), cts.Token))
            .ReturnsAsync("result");

        var agent = new SubAgentInstance("id", "Name", new HashSet<string>(), mockModel.Object);

        await agent.ExecuteTaskAsync("task", cts.Token);

        mockModel.Verify(m => m.GenerateTextAsync(It.IsAny<string>(), cts.Token), Times.Once);
    }
}
