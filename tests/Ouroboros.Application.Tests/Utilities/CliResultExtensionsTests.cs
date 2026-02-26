using FluentAssertions;
using Ouroboros.Application.Utilities;
using Xunit;

namespace Ouroboros.Tests.Utilities;

[Trait("Category", "Unit")]
public class CliResultExtensionsTests
{
    [Fact]
    public async Task MatchAsync_Success_ShouldInvokeSuccessHandler()
    {
        var result = Result<int>.Success(42);

        var output = await result.MatchAsync(
            success: v => Task.FromResult($"ok:{v}"),
            failure: e => Task.FromResult($"err:{e}"));

        output.Should().Be("ok:42");
    }

    [Fact]
    public async Task MatchAsync_Failure_ShouldInvokeFailureHandler()
    {
        var result = Result<int>.Failure("bad input");

        var output = await result.MatchAsync(
            success: v => Task.FromResult($"ok:{v}"),
            failure: e => Task.FromResult($"err:{e}"));

        output.Should().Be("err:bad input");
    }
}
