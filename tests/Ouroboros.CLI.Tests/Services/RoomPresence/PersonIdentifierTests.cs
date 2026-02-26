using Ouroboros.CLI.Services.RoomPresence;

namespace Ouroboros.Tests.CLI.Services.RoomPresence;

[Trait("Category", "Unit")]
public class PersonIdentifierTests
{
    [Theory]
    [InlineData("I'm Alice", "Alice")]
    [InlineData("My name is Bob", "Bob")]
    [InlineData("Call me Charlie", "Charlie")]
    [InlineData("I am Diana", "Diana")]
    [InlineData("im Evan", "Evan")]
    public void ExtractIntroductionName_ValidIntroductions_ReturnsName(string text, string expected)
    {
        var name = PersonIdentifier.ExtractIntroductionName(text);

        name.Should().Be(expected);
    }

    [Theory]
    [InlineData("Hello, how are you?")]
    [InlineData("The weather is nice")]
    [InlineData("")]
    [InlineData("I am")]
    [InlineData("my name is")]
    public void ExtractIntroductionName_NoIntroduction_ReturnsNull(string text)
    {
        var name = PersonIdentifier.ExtractIntroductionName(text);

        name.Should().BeNull();
    }

    [Theory]
    [InlineData("I'm a developer")]
    [InlineData("My name is not important")]
    public void ExtractIntroductionName_NonNameFollowingIntro_BehavesReasonably(string text)
    {
        // These may or may not extract something depending on regex
        // The important thing is no exception is thrown
        var action = () => PersonIdentifier.ExtractIntroductionName(text);
        action.Should().NotThrow();
    }
}
