using Ouroboros.CLI;

namespace Ouroboros.Tests.CLI.Commands.Options;

[Trait("Category", "Unit")]
public class OptionBridgeTests
{
    [Fact]
    public void Maybe_WithSomeValue_HasValue()
    {
        var option = Ouroboros.Abstractions.Monads.Option<int>.Some(42);
        Maybe<int> maybe = option;

        maybe.HasValue.Should().BeTrue();
        maybe.Value.Should().Be(42);
    }

    [Fact]
    public void Maybe_WithNone_HasNoValue()
    {
        var option = Ouroboros.Abstractions.Monads.Option<int>.None();
        Maybe<int> maybe = option;

        maybe.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Maybe_GetValueOrDefault_ReturnsSomeValue()
    {
        var option = Ouroboros.Abstractions.Monads.Option<string>.Some("hello");
        Maybe<string> maybe = option;

        maybe.GetValueOrDefault("default").Should().Be("hello");
    }

    [Fact]
    public void Maybe_GetValueOrDefault_ReturnsDefaultForNone()
    {
        var option = Ouroboros.Abstractions.Monads.Option<string>.None();
        Maybe<string> maybe = option;

        maybe.GetValueOrDefault("default").Should().Be("default");
    }

    [Fact]
    public void Maybe_ToString_WithSome_ShowsSome()
    {
        var option = Ouroboros.Abstractions.Monads.Option<int>.Some(42);
        Maybe<int> maybe = option;

        maybe.ToString().Should().Be("Some(42)");
    }

    [Fact]
    public void Maybe_ToString_WithNone_ShowsNone()
    {
        var option = Ouroboros.Abstractions.Monads.Option<int>.None();
        Maybe<int> maybe = option;

        maybe.ToString().Should().Be("None");
    }

    [Fact]
    public void Maybe_ImplicitConversion_RoundTrips()
    {
        var original = Ouroboros.Abstractions.Monads.Option<int>.Some(99);
        Maybe<int> maybe = original;
        Ouroboros.Abstractions.Monads.Option<int> backToOption = maybe;

        backToOption.HasValue.Should().BeTrue();
        backToOption.Value.Should().Be(99);
    }
}
