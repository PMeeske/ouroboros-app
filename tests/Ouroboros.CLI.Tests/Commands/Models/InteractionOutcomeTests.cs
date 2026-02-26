using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Models;

[Trait("Category", "Unit")]
public class InteractionOutcomeTests
{
    [Fact]
    public void Constructor_SetsAllRequiredProperties()
    {
        var outcome = new InteractionOutcome(
            UserInput: "search for auth",
            AgentResponse: "[TOOL:search_my_code auth]",
            ExpectedTools: new List<string> { "search_my_code" },
            ActualToolCalls: new List<string> { "search_my_code" },
            WasSuccessful: true,
            ResponseTime: TimeSpan.FromMilliseconds(500));

        outcome.UserInput.Should().Be("search for auth");
        outcome.AgentResponse.Should().Be("[TOOL:search_my_code auth]");
        outcome.ExpectedTools.Should().ContainSingle("search_my_code");
        outcome.ActualToolCalls.Should().ContainSingle("search_my_code");
        outcome.WasSuccessful.Should().BeTrue();
        outcome.ResponseTime.Should().Be(TimeSpan.FromMilliseconds(500));
        outcome.UserFeedback.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithOptionalFeedback_SetsFeedback()
    {
        var outcome = new InteractionOutcome(
            UserInput: "test",
            AgentResponse: "response",
            ExpectedTools: new List<string>(),
            ActualToolCalls: new List<string>(),
            WasSuccessful: false,
            ResponseTime: TimeSpan.FromSeconds(1),
            UserFeedback: "not helpful");

        outcome.UserFeedback.Should().Be("not helpful");
    }

    [Fact]
    public void DefaultFeedback_IsNull()
    {
        var outcome = new InteractionOutcome(
            "input", "response",
            new List<string>(), new List<string>(),
            true, TimeSpan.Zero);

        outcome.UserFeedback.Should().BeNull();
    }
}
