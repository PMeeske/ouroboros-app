using FluentAssertions;
using Ouroboros.Application.Services;
using Xunit;
using ConversationTurn = Ouroboros.Application.Services.ConversationTurn;
using ValidationResult = Ouroboros.Application.Services.ValidationResult;

namespace Ouroboros.Tests.Services;

[Trait("Category", "Unit")]
public class ServiceModelsTests
{
    [Fact]
    public void Thought_ShouldSetProperties()
    {
        var thought = new Thought
        {
            Timestamp = DateTime.UtcNow,
            Prompt = "What is life?",
            Content = "A philosophical question",
            Type = ThoughtType.Reflection
        };

        thought.Prompt.Should().Be("What is life?");
        thought.Content.Should().Be("A philosophical question");
        thought.Type.Should().Be(ThoughtType.Reflection);
    }

    [Fact]
    public void ThoughtType_ShouldHave6Values()
    {
        Enum.GetValues<ThoughtType>().Should().HaveCount(6);
    }

    [Fact]
    public void ConversationTurn_ShouldSetProperties()
    {
        var turn = new ConversationTurn("user", "Hello", DateTime.UtcNow, "session-1");

        turn.Role.Should().Be("user");
        turn.Content.Should().Be("Hello");
        turn.SessionId.Should().Be("session-1");
        turn.Metadata.Should().BeNull();
    }

    [Fact]
    public void SearchResult_ShouldSetProperties()
    {
        var result = new SearchResult
        {
            FilePath = "/src/test.cs",
            ChunkIndex = 3,
            Content = "test content",
            Score = 0.95f
        };

        result.FilePath.Should().Be("/src/test.cs");
        result.ChunkIndex.Should().Be(3);
        result.Score.Should().Be(0.95f);
    }

    [Fact]
    public void ValidationResult_ShouldSetProperties()
    {
        var result = new ValidationResult
        {
            Success = true,
            AllPassed = true,
            Details = "All checks passed",
            Expectations = new[] { "Button visible", "Text matches" },
            Timestamp = DateTime.UtcNow
        };

        result.Success.Should().BeTrue();
        result.AllPassed.Should().BeTrue();
        result.Expectations.Should().HaveCount(2);
    }

    [Fact]
    public void PresenceState_ShouldHave3Values()
    {
        Enum.GetValues<PresenceState>().Should().HaveCount(3);
    }
}
