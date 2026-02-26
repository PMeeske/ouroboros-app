using FluentAssertions;
using Ouroboros.Application.Agent;
using Xunit;

namespace Ouroboros.Tests.Agent;

[Trait("Category", "Unit")]
public class AutoAgentConfigTests
{
    [Fact]
    public void Parse_NullArgs_ShouldReturnDefaults()
    {
        var config = AutoAgentConfig.Parse(null);

        config.Task.Should().BeNull();
        config.MaxIterations.Should().Be(15);
    }

    [Fact]
    public void Parse_EmptyArgs_ShouldReturnDefaults()
    {
        var config = AutoAgentConfig.Parse("");

        config.Task.Should().BeNull();
        config.MaxIterations.Should().Be(15);
    }

    [Fact]
    public void Parse_WhitespaceArgs_ShouldReturnDefaults()
    {
        var config = AutoAgentConfig.Parse("   ");

        config.Task.Should().BeNull();
        config.MaxIterations.Should().Be(15);
    }

    [Fact]
    public void Parse_TaskOnly_ShouldSetTask()
    {
        var config = AutoAgentConfig.Parse("fix the bug");

        config.Task.Should().Be("fix the bug");
        config.MaxIterations.Should().Be(15);
    }

    [Fact]
    public void Parse_MaxIterOnly_ShouldSetMaxIterations()
    {
        var config = AutoAgentConfig.Parse("maxIter=25");

        config.MaxIterations.Should().Be(25);
    }

    [Fact]
    public void Parse_TaskAndMaxIter_ShouldSetBoth()
    {
        var config = AutoAgentConfig.Parse("fix the bug;maxIter=10");

        config.Task.Should().Be("fix the bug");
        config.MaxIterations.Should().Be(10);
    }

    [Fact]
    public void Parse_SingleQuotedArgs_ShouldRemoveQuotes()
    {
        var config = AutoAgentConfig.Parse("'fix the bug;maxIter=10'");

        config.Task.Should().Be("fix the bug");
        config.MaxIterations.Should().Be(10);
    }

    [Fact]
    public void Parse_DoubleQuotedArgs_ShouldRemoveQuotes()
    {
        var config = AutoAgentConfig.Parse("\"fix the bug;maxIter=10\"");

        config.Task.Should().Be("fix the bug");
        config.MaxIterations.Should().Be(10);
    }

    [Fact]
    public void Parse_InvalidMaxIter_ShouldKeepDefault()
    {
        var config = AutoAgentConfig.Parse("maxIter=abc");

        config.MaxIterations.Should().Be(15);
    }

    [Fact]
    public void Parse_CaseInsensitiveMaxIter_ShouldParse()
    {
        var config = AutoAgentConfig.Parse("MAXITER=20");

        config.MaxIterations.Should().Be(20);
    }
}
