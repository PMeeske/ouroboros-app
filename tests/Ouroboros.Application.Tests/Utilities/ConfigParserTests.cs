using FluentAssertions;
using Ouroboros.Application.Utilities;
using Xunit;

namespace Ouroboros.Tests.Utilities;

[Trait("Category", "Unit")]
public class ConfigParserTests
{
    // --- ParseKeyValueArgs ---

    [Fact]
    public void ParseKeyValueArgs_Null_ShouldReturnEmptyDictionary()
    {
        var result = ConfigParser.ParseKeyValueArgs(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseKeyValueArgs_Empty_ShouldReturnEmptyDictionary()
    {
        var result = ConfigParser.ParseKeyValueArgs("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseKeyValueArgs_SingleKeyValue_ShouldParse()
    {
        var result = ConfigParser.ParseKeyValueArgs("key=value");

        result.Should().ContainKey("key").WhoseValue.Should().Be("value");
    }

    [Fact]
    public void ParseKeyValueArgs_MultiplePipeDelimited_ShouldParseAll()
    {
        var result = ConfigParser.ParseKeyValueArgs("a=1|b=2|c=3");

        result.Should().HaveCount(3);
        result["a"].Should().Be("1");
        result["b"].Should().Be("2");
        result["c"].Should().Be("3");
    }

    [Fact]
    public void ParseKeyValueArgs_FlagWithoutValue_ShouldDefaultToTrue()
    {
        var result = ConfigParser.ParseKeyValueArgs("verbose");

        result.Should().ContainKey("verbose").WhoseValue.Should().Be("true");
    }

    [Fact]
    public void ParseKeyValueArgs_CaseInsensitiveKeys()
    {
        var result = ConfigParser.ParseKeyValueArgs("Key=value");

        result.Should().ContainKey("KEY");
        result.Should().ContainKey("key");
    }

    [Fact]
    public void ParseKeyValueArgs_TrimsWhitespace()
    {
        var result = ConfigParser.ParseKeyValueArgs(" key = value ");

        result.Should().ContainKey("key").WhoseValue.Should().Be("value");
    }

    // --- ParseString ---

    [Fact]
    public void ParseString_Null_ShouldReturnEmpty()
    {
        var result = ConfigParser.ParseString(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseString_Empty_ShouldReturnEmpty()
    {
        var result = ConfigParser.ParseString("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseString_SingleQuoted_ShouldUnwrap()
    {
        var result = ConfigParser.ParseString("'hello world'");

        result.Should().Be("hello world");
    }

    [Fact]
    public void ParseString_DoubleQuoted_ShouldUnwrap()
    {
        var result = ConfigParser.ParseString("\"hello world\"");

        result.Should().Be("hello world");
    }

    [Fact]
    public void ParseString_Unquoted_ShouldReturnTrimmed()
    {
        var result = ConfigParser.ParseString("  hello  ");

        result.Should().Be("hello");
    }

    [Fact]
    public void ParseString_SingleChar_ShouldReturnAsIs()
    {
        var result = ConfigParser.ParseString("x");

        result.Should().Be("x");
    }

    // --- ParseBool ---

    [Fact]
    public void ParseBool_Null_ShouldReturnDefault()
    {
        ConfigParser.ParseBool(null).Should().BeFalse();
        ConfigParser.ParseBool(null, true).Should().BeTrue();
    }

    [Fact]
    public void ParseBool_TrueString_ShouldReturnTrue()
    {
        ConfigParser.ParseBool("true").Should().BeTrue();
        ConfigParser.ParseBool("True").Should().BeTrue();
    }

    [Fact]
    public void ParseBool_FalseString_ShouldReturnFalse()
    {
        ConfigParser.ParseBool("false").Should().BeFalse();
    }

    [Fact]
    public void ParseBool_NumericOne_ShouldReturnTrue()
    {
        ConfigParser.ParseBool("1").Should().BeTrue();
    }

    [Fact]
    public void ParseBool_NumericZero_ShouldReturnFalse()
    {
        ConfigParser.ParseBool("0").Should().BeFalse();
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("y")]
    [InlineData("on")]
    [InlineData("enable")]
    [InlineData("enabled")]
    public void ParseBool_PositiveKeywords_ShouldReturnTrue(string input)
    {
        ConfigParser.ParseBool(input).Should().BeTrue();
    }

    [Fact]
    public void ParseBool_UnrecognizedString_ShouldReturnFalse()
    {
        ConfigParser.ParseBool("banana").Should().BeFalse();
    }

    // --- ChooseFirstNonEmpty ---

    [Fact]
    public void ChooseFirstNonEmpty_AllNull_ShouldReturnNull()
    {
        var result = ConfigParser.ChooseFirstNonEmpty(null, null, null);

        result.Should().BeNull();
    }

    [Fact]
    public void ChooseFirstNonEmpty_FirstNonEmpty_ShouldReturnIt()
    {
        var result = ConfigParser.ChooseFirstNonEmpty(null, "", "   ", "hello", "world");

        result.Should().Be("hello");
    }

    [Fact]
    public void ChooseFirstNonEmpty_FirstIsValid_ShouldReturnFirst()
    {
        var result = ConfigParser.ChooseFirstNonEmpty("first", "second");

        result.Should().Be("first");
    }

    // --- Parse<TConfig> ---

    [Fact]
    public void Parse_NullArgs_ShouldInvokeBuilderWithEmptyDict()
    {
        var result = ConfigParser.Parse<string>(
            null,
            "default",
            (dict, defaults) =>
            {
                return Result<string>.Success(defaults);
            });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("default");
    }

    [Fact]
    public void Parse_ValidArgs_ShouldPassParsedDict()
    {
        var result = ConfigParser.Parse<string>(
            "name=test",
            "default",
            (dict, defaults) =>
            {
                return Result<string>.Success(dict["name"]);
            });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test");
    }

    [Fact]
    public void Parse_BuilderThrows_ShouldReturnFailure()
    {
        var result = ConfigParser.Parse<string>(
            "a=b",
            "default",
            (dict, defaults) => throw new InvalidOperationException("boom"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("boom");
    }
}
