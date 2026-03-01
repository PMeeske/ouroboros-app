using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality.Models;

[Trait("Category", "Unit")]
public class InteractionFeedbackTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        var feedback = new InteractionFeedback(0.8, 0.9, 0.7, 0.85, "coding", "How?", true);

        feedback.EngagementLevel.Should().Be(0.8);
        feedback.ResponseRelevance.Should().Be(0.9);
        feedback.QuestionQuality.Should().Be(0.7);
        feedback.ConversationContinuity.Should().Be(0.85);
        feedback.TopicDiscussed.Should().Be("coding");
        feedback.QuestionAsked.Should().Be("How?");
        feedback.UserAskedFollowUp.Should().BeTrue();
    }
}
