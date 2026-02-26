using Ouroboros.CLI.Services;

namespace Ouroboros.Tests.CLI.Services;

[Trait("Category", "Unit")]
public class AskRequestTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var request = new AskRequest("What is 2+2?");

        request.Question.Should().Be("What is 2+2?");
        request.UseRag.Should().BeFalse();
        request.ModelName.Should().Be("deepseek-v3.1:671b-cloud");
        request.Endpoint.Should().BeNull();
        request.ApiKey.Should().BeNull();
        request.EndpointType.Should().BeNull();
        request.Temperature.Should().Be(0.7);
        request.MaxTokens.Should().Be(2048);
        request.TimeoutSeconds.Should().Be(60);
        request.Stream.Should().BeFalse();
        request.Culture.Should().BeNull();
        request.AgentMode.Should().BeFalse();
        request.AgentModeType.Should().Be("lc");
        request.AgentMaxSteps.Should().Be(6);
        request.StrictModel.Should().BeFalse();
        request.Router.Should().Be("direct");
        request.EmbedModel.Should().Be("nomic-embed-text");
        request.TopK.Should().Be(3);
        request.Debug.Should().BeFalse();
        request.JsonTools.Should().BeFalse();
        request.Persona.Should().Be("Iaret");
        request.VoiceOnly.Should().BeFalse();
        request.LocalTts.Should().BeFalse();
        request.VoiceLoop.Should().BeFalse();
    }

    [Fact]
    public void WithCustomValues_SetsAllProperties()
    {
        var request = new AskRequest(
            Question: "Test",
            UseRag: true,
            ModelName: "gpt-4",
            Temperature: 0.9,
            MaxTokens: 4096,
            AgentMode: true,
            Debug: true);

        request.UseRag.Should().BeTrue();
        request.ModelName.Should().Be("gpt-4");
        request.Temperature.Should().Be(0.9);
        request.MaxTokens.Should().Be(4096);
        request.AgentMode.Should().BeTrue();
        request.Debug.Should().BeTrue();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var r1 = new AskRequest("test");
        var r2 = new AskRequest("test");

        r1.Should().Be(r2);
    }

    [Fact]
    public void Equality_DifferentQuestions_AreNotEqual()
    {
        var r1 = new AskRequest("question1");
        var r2 = new AskRequest("question2");

        r1.Should().NotBe(r2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var request = new AskRequest("original");
        var modified = request with { Question = "modified", Debug = true };

        modified.Question.Should().Be("modified");
        modified.Debug.Should().BeTrue();
        request.Question.Should().Be("original");
    }
}
