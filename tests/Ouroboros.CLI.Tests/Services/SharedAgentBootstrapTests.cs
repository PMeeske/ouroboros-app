using Ouroboros.CLI.Services;

namespace Ouroboros.Tests.CLI.Services;

[Trait("Category", "Unit")]
public class SharedAgentBootstrapTests
{
    [Fact]
    public void CreateEmbeddingModel_InvalidEndpoint_ReturnsNull()
    {
        // With an unreachable endpoint, the factory should catch the error and return null
        var result = SharedAgentBootstrap.CreateEmbeddingModel(
            "http://invalid-host:99999", "nomic-embed-text");

        result.Should().BeNull();
    }

    [Fact]
    public void CreateEmbeddingModel_InvalidEndpoint_CallsLog()
    {
        string? logMessage = null;

        SharedAgentBootstrap.CreateEmbeddingModel(
            "http://invalid-host:99999", "nomic-embed-text",
            log: msg => logMessage = msg);

        // Should have either logged success or unavailable
        logMessage.Should().NotBeNull();
    }

    [Fact]
    public void CreateEpisodicMemory_NullEmbedding_ReturnsNull()
    {
        var result = SharedAgentBootstrap.CreateEpisodicMemory(
            "http://localhost:6334", null);

        result.Should().BeNull();
    }

    [Fact]
    public void CreateNeuralSymbolicBridge_NullChatModel_ReturnsNull()
    {
        var mockMeTTa = new Moq.Mock<Ouroboros.Tools.MeTTa.IMeTTaEngine>();

        var result = SharedAgentBootstrap.CreateNeuralSymbolicBridge(null, mockMeTTa.Object);

        result.Should().BeNull();
    }

    [Fact]
    public void CreateCognitivePhysics_ReturnsNonNull()
    {
        var (engine, state) = SharedAgentBootstrap.CreateCognitivePhysics();

        engine.Should().NotBeNull();
        state.Should().NotBeNull();
    }
}
