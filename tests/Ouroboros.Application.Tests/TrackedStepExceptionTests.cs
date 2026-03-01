using FluentAssertions;
using Moq;
using Ouroboros.Application;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class TrackedStepExceptionTests
{
    [Fact]
    public void Constructor_ShouldSetMessageAndInner()
    {
        var inner = new InvalidOperationException("inner error");
        // Sealed types — use null! since test only checks exception properties
        var mockState = new CliPipelineState
        {
            Branch = null!,
            Llm = null!,
            Tools = null!,
            Embed = Mock.Of<Ouroboros.Domain.IEmbeddingModel>()
        };

        var ex = new TrackedStepException("step failed", inner, mockState);

        ex.Message.Should().Be("step failed");
        ex.InnerException.Should().BeSameAs(inner);
        ex.State.Should().BeSameAs(mockState);
    }
}
