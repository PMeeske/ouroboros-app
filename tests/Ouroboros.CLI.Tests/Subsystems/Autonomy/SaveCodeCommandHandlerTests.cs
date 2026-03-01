// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
using Moq;
using Ouroboros.CLI.Subsystems.Autonomy;
using Ouroboros.Tools;
using OptionT = Ouroboros.Abstractions.Monads.Option<Ouroboros.Tools.ITool>;

namespace Ouroboros.Tests.CLI.Subsystems.Autonomy;

[Trait("Category", "Unit")]
public class SaveCodeCommandHandlerTests
{
    // ═══════════════════════════════════════════════════════════════
    // ParseArgument – JSON input
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ParseArgument_JsonInput_ReturnsUnchanged()
    {
        const string json = """{"file":"src/Foo.cs","search":"old","replace":"new"}""";

        var result = SaveCodeCommandHandler.ParseArgument(json);

        result.Should().Be(json);
    }

    [Fact]
    public void ParseArgument_JsonWithLeadingWhitespace_ReturnsUnchanged()
    {
        const string json = """  {"file":"a.cs","search":"x","replace":"y"}""";

        var result = SaveCodeCommandHandler.ParseArgument(json);

        result.Should().Be(json);
    }

    // ═══════════════════════════════════════════════════════════════
    // ParseArgument – positional format (file "search" "replace")
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ParseArgument_DoubleQuotedPositional_BuildsCorrectJson()
    {
        const string input = """MyClass.cs "public void Old()" "public void New()" """;

        var result = SaveCodeCommandHandler.ParseArgument(input);

        result.Should().Contain("\"file\":\"MyClass.cs\"");
        result.Should().Contain("\"search\":\"public void Old()\"");
        result.Should().Contain("\"replace\":\"public void New()\"");
    }

    [Fact]
    public void ParseArgument_SingleQuotedPositional_BuildsCorrectJson()
    {
        const string input = "MyClass.cs 'old text' 'new text'";

        var result = SaveCodeCommandHandler.ParseArgument(input);

        result.Should().Contain("\"file\":\"MyClass.cs\"");
        result.Should().Contain("\"search\":\"old text\"");
        result.Should().Contain("\"replace\":\"new text\"");
    }

    // ═══════════════════════════════════════════════════════════════
    // NormalizeSmartQuotes
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("\u201Chello\u201D", "\"hello\"")]     // Left + right smart double quotes
    [InlineData("\u201Ehello\u201F", "\"hello\"")]     // German low + high-reversed-9
    [InlineData("\u2018hi\u2019", "'hi'")]             // Left + right single smart quotes
    [InlineData("`backtick`", "'backtick'")]           // Backtick normalization
    public void NormalizeSmartQuotes_ReplacesUnicodeVariants(string input, string expected)
    {
        var result = SaveCodeCommandHandler.NormalizeSmartQuotes(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeSmartQuotes_PlainAscii_ReturnsSameString()
    {
        const string input = "no special quotes here";

        var result = SaveCodeCommandHandler.NormalizeSmartQuotes(input);

        result.Should().Be(input);
    }

    // ═══════════════════════════════════════════════════════════════
    // ParseQuotedStrings
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ParseQuotedStrings_TwoDoubleQuotedSegments_ReturnsBoth()
    {
        const string input = "\"first segment\" \"second segment\"";

        var result = SaveCodeCommandHandler.ParseQuotedStrings(input, '"');

        result.Should().HaveCount(2);
        result[0].Should().Be("first segment");
        result[1].Should().Be("second segment");
    }

    [Fact]
    public void ParseQuotedStrings_SingleQuoteChar_ParsesCorrectly()
    {
        const string input = "'search text' 'replace text'";

        var result = SaveCodeCommandHandler.ParseQuotedStrings(input, '\'');

        result.Should().HaveCount(2);
        result[0].Should().Be("search text");
        result[1].Should().Be("replace text");
    }

    [Fact]
    public void ParseQuotedStrings_NoQuotes_ReturnsEmpty()
    {
        const string input = "no quotes at all";

        var result = SaveCodeCommandHandler.ParseQuotedStrings(input, '"');

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseQuotedStrings_OnlyOneQuotedSegment_ReturnsSingle()
    {
        const string input = "\"only one\" unquoted stuff";

        var result = SaveCodeCommandHandler.ParseQuotedStrings(input, '"');

        result.Should().ContainSingle().Which.Should().Be("only one");
    }

    // ═══════════════════════════════════════════════════════════════
    // BuildJsonInput
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void BuildJsonInput_ProducesValidJson()
    {
        var json = SaveCodeCommandHandler.BuildJsonInput("src/A.cs", "old", "new");

        json.Should().Contain("\"file\":\"src/A.cs\"");
        json.Should().Contain("\"search\":\"old\"");
        json.Should().Contain("\"replace\":\"new\"");
    }

    // ═══════════════════════════════════════════════════════════════
    // Error handling
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ParseArgument_NoQuotes_ThrowsFormatException()
    {
        const string input = "MyClass.cs old text new text";

        var act = () => SaveCodeCommandHandler.ParseArgument(input);

        act.Should().Throw<FormatException>()
            .WithMessage("*Invalid format*");
    }

    [Fact]
    public void ParseArgument_OnlyOneQuotedSegment_ThrowsFormatException()
    {
        const string input = "MyClass.cs \"only search text\"";

        var act = () => SaveCodeCommandHandler.ParseArgument(input);

        act.Should().Throw<FormatException>()
            .WithMessage("*Could not parse*1 quoted section*");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyArgument_WithToolAvailable_ReturnsUsageText()
    {
        var mockTool = new Mock<ITool>();
        var handler = new SaveCodeCommandHandler(_ => OptionT.Some(mockTool.Object));

        var result = await handler.ExecuteAsync("");

        result.Should().Contain("Direct Tool Invocation");
    }

    [Fact]
    public async Task ExecuteAsync_NullArgument_WithToolAvailable_ReturnsUsageText()
    {
        var mockTool = new Mock<ITool>();
        var handler = new SaveCodeCommandHandler(_ => OptionT.Some(mockTool.Object));

        var result = await handler.ExecuteAsync(null!);

        result.Should().Contain("Direct Tool Invocation");
    }

    [Fact]
    public async Task ExecuteAsync_ToolNotRegistered_ReturnsErrorMessage()
    {
        var handler = new SaveCodeCommandHandler(_ => OptionT.None());

        var result = await handler.ExecuteAsync("""{"file":"a.cs","search":"x","replace":"y"}""");

        result.Should().Contain("modify_my_code tool is not registered");
    }
}
