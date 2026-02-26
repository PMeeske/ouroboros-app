using FluentAssertions;
using Ouroboros.Application.Configuration;
using Xunit;

namespace Ouroboros.Tests.Configuration;

[Trait("Category", "Unit")]
public class StateKeysTests
{
    [Fact]
    public void Text_ShouldBeExpectedValue()
    {
        StateKeys.Text.Should().Be("text");
    }

    [Fact]
    public void Context_ShouldBeExpectedValue()
    {
        StateKeys.Context.Should().Be("context");
    }

    [Fact]
    public void Question_ShouldBeExpectedValue()
    {
        StateKeys.Question.Should().Be("question");
    }

    [Fact]
    public void Prompt_ShouldBeExpectedValue()
    {
        StateKeys.Prompt.Should().Be("prompt");
    }

    [Fact]
    public void Topic_ShouldBeExpectedValue()
    {
        StateKeys.Topic.Should().Be("topic");
    }

    [Fact]
    public void Query_ShouldBeExpectedValue()
    {
        StateKeys.Query.Should().Be("query");
    }

    [Fact]
    public void Input_ShouldBeExpectedValue()
    {
        StateKeys.Input.Should().Be("input");
    }

    [Fact]
    public void Output_ShouldBeExpectedValue()
    {
        StateKeys.Output.Should().Be("output");
    }

    [Fact]
    public void Documents_ShouldBeExpectedValue()
    {
        StateKeys.Documents.Should().Be("documents");
    }
}
