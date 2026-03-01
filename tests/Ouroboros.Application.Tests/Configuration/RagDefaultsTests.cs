using FluentAssertions;
using Ouroboros.Application.Configuration;
using Xunit;

namespace Ouroboros.Tests.Configuration;

[Trait("Category", "Unit")]
public class RagDefaultsTests
{
    [Fact]
    public void GroupSize_ShouldBe6()
    {
        RagDefaults.GroupSize.Should().Be(6);
    }

    [Fact]
    public void SubQuestions_ShouldBe4()
    {
        RagDefaults.SubQuestions.Should().Be(4);
    }

    [Fact]
    public void DocumentsPerSubQuestion_ShouldBe6()
    {
        RagDefaults.DocumentsPerSubQuestion.Should().Be(6);
    }
}
