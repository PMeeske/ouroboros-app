using Ouroboros.CLI.Avatar;

namespace Ouroboros.Tests.CLI.Avatar;

[Trait("Category", "Unit")]
public class IaretCliAvatarTests
{
    [Theory]
    [InlineData(IaretCliAvatar.Expression.Idle, "☥(◉ ◉)")]
    [InlineData(IaretCliAvatar.Expression.Wink, "☥(◉ ‿)")]
    [InlineData(IaretCliAvatar.Expression.Thinking, "☥(● ●)")]
    [InlineData(IaretCliAvatar.Expression.Speaking, "☥(◉ ◉)°")]
    [InlineData(IaretCliAvatar.Expression.Happy, "☥(◡ ◡)")]
    [InlineData(IaretCliAvatar.Expression.Listening, "☥(◎ ◎)")]
    [InlineData(IaretCliAvatar.Expression.Concerned, "☥(◉ ◉)~")]
    [InlineData(IaretCliAvatar.Expression.Playful, "☥(◉ ▿)")]
    public void Inline_ReturnsCorrectExpression(IaretCliAvatar.Expression expr, string expected)
    {
        IaretCliAvatar.Inline(expr).Should().Be(expected);
    }

    [Fact]
    public void Standard_ReturnsThreeLines()
    {
        var lines = IaretCliAvatar.Standard(IaretCliAvatar.Expression.Idle);

        lines.Should().HaveCount(3);
        lines[0].Should().Contain("☥");
    }

    [Fact]
    public void Banner_ReturnsFiveLines()
    {
        var lines = IaretCliAvatar.Banner(IaretCliAvatar.Expression.Happy);

        lines.Should().HaveCount(5);
        lines[1].Should().Contain("☥");
    }

    [Theory]
    [InlineData("error", IaretCliAvatar.Expression.Concerned)]
    [InlineData("fail", IaretCliAvatar.Expression.Concerned)]
    [InlineData("failure", IaretCliAvatar.Expression.Concerned)]
    [InlineData("warning", IaretCliAvatar.Expression.Concerned)]
    [InlineData("success", IaretCliAvatar.Expression.Happy)]
    [InlineData("done", IaretCliAvatar.Expression.Happy)]
    [InlineData("complete", IaretCliAvatar.Expression.Happy)]
    [InlineData("thinking", IaretCliAvatar.Expression.Thinking)]
    [InlineData("processing", IaretCliAvatar.Expression.Thinking)]
    [InlineData("speaking", IaretCliAvatar.Expression.Speaking)]
    [InlineData("listening", IaretCliAvatar.Expression.Listening)]
    public void ForContext_ReturnsDeterministicExpressions(string context, IaretCliAvatar.Expression expected)
    {
        // Run multiple times; the deterministic path should dominate (~88% of the time)
        var results = Enumerable.Range(0, 100)
            .Select(_ => IaretCliAvatar.ForContext(context))
            .ToList();

        // The expected expression should appear the majority of the time
        results.Count(r => r == expected).Should().BeGreaterThanOrEqualTo(80);
    }

    [Fact]
    public void ForContext_UnknownContext_ReturnsIdle()
    {
        // Mostly Idle for unknown context, but with some spontaneous wink/playful
        var results = Enumerable.Range(0, 100)
            .Select(_ => IaretCliAvatar.ForContext("unknown_context_xyz"))
            .ToList();

        results.Count(r => r == IaretCliAvatar.Expression.Idle).Should().BeGreaterThanOrEqualTo(80);
    }

    [Fact]
    public void Random_ReturnsValidExpression()
    {
        var result = IaretCliAvatar.Random();

        result.Should().BeOneOf(
            IaretCliAvatar.Expression.Idle,
            IaretCliAvatar.Expression.Happy,
            IaretCliAvatar.Expression.Wink,
            IaretCliAvatar.Expression.Listening,
            IaretCliAvatar.Expression.Playful,
            IaretCliAvatar.Expression.Thinking);
    }

    [Fact]
    public void Expression_EnumHasAllExpectedValues()
    {
        var values = Enum.GetValues<IaretCliAvatar.Expression>();

        values.Should().HaveCount(8);
        values.Should().Contain(IaretCliAvatar.Expression.Idle);
        values.Should().Contain(IaretCliAvatar.Expression.Wink);
        values.Should().Contain(IaretCliAvatar.Expression.Thinking);
        values.Should().Contain(IaretCliAvatar.Expression.Speaking);
        values.Should().Contain(IaretCliAvatar.Expression.Happy);
        values.Should().Contain(IaretCliAvatar.Expression.Listening);
        values.Should().Contain(IaretCliAvatar.Expression.Concerned);
        values.Should().Contain(IaretCliAvatar.Expression.Playful);
    }
}
