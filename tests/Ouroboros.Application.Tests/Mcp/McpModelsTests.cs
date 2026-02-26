using FluentAssertions;
using Ouroboros.Application.Mcp;
using Xunit;

namespace Ouroboros.Tests.Mcp;

[Trait("Category", "Unit")]
public class McpModelsTests
{
    // --- McpRequest ---

    [Fact]
    public void McpRequest_ShouldHaveDefaultJsonRpc()
    {
        var request = new McpRequest { Method = "tools/list" };

        request.JsonRpc.Should().Be("2.0");
    }

    [Fact]
    public void McpRequest_ShouldSetProperties()
    {
        var request = new McpRequest
        {
            Id = 1,
            Method = "tools/call",
            Params = new { name = "search" }
        };

        request.Id.Should().Be(1);
        request.Method.Should().Be("tools/call");
        request.Params.Should().NotBeNull();
    }

    [Fact]
    public void McpRequest_Id_ShouldBeNullable()
    {
        var request = new McpRequest { Method = "notifications/progress" };

        request.Id.Should().BeNull();
    }

    // --- McpToolInfo ---

    [Fact]
    public void McpToolInfo_ShouldSetProperties()
    {
        var info = new McpToolInfo
        {
            Name = "search",
            Description = "Search the web",
            InputSchema = "{\"type\":\"object\"}"
        };

        info.Name.Should().Be("search");
        info.Description.Should().Be("Search the web");
        info.InputSchema.Should().Contain("object");
    }

    // --- McpToolResult ---

    [Fact]
    public void McpToolResult_Success_ShouldSetProperties()
    {
        var result = new McpToolResult
        {
            IsError = false,
            Content = "Search results here"
        };

        result.IsError.Should().BeFalse();
        result.Content.Should().Be("Search results here");
    }

    [Fact]
    public void McpToolResult_Error_ShouldSetProperties()
    {
        var result = new McpToolResult
        {
            IsError = true,
            Content = "Tool not found"
        };

        result.IsError.Should().BeTrue();
        result.Content.Should().Be("Tool not found");
    }

    // --- PlaywrightArgs ---

    [Fact]
    public void PlaywrightArgs_ShouldHaveDefaults()
    {
        var args = new PlaywrightArgs();

        args.Action.Should().BeEmpty();
        args.Url.Should().BeNull();
        args.Element.Should().BeNull();
        args.Ref.Should().BeNull();
        args.Text.Should().BeNull();
        args.Code.Should().BeNull();
        args.FullPage.Should().BeFalse();
        args.Goal.Should().BeNull();
        args.Expectations.Should().BeNull();
    }

    [Fact]
    public void PlaywrightArgs_Validate_EmptyAction_ShouldFail()
    {
        var args = new PlaywrightArgs();
        var (isValid, error) = args.Validate();

        isValid.Should().BeFalse();
        error.Should().Contain("Action is required");
    }

    [Fact]
    public void PlaywrightArgs_Validate_ValidAction_ShouldPass()
    {
        var args = new PlaywrightArgs { Action = "navigate", Url = "https://example.com" };
        var (isValid, error) = args.Validate();

        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void PlaywrightArgs_Validate_PlaceholderUrl_ShouldFail()
    {
        var args = new PlaywrightArgs { Action = "navigate", Url = "URL of the result page" };
        var (isValid, error) = args.Validate();

        isValid.Should().BeFalse();
        error.Should().Contain("placeholder");
    }

    [Fact]
    public void PlaywrightArgs_Validate_LongRef_ShouldFail()
    {
        // Use a ref that is long but does not trigger placeholder detection
        var args = new PlaywrightArgs { Action = "click", Ref = "elementRefThatIsTooLongForUse" };
        var (isValid, error) = args.Validate();

        isValid.Should().BeFalse();
        error.Should().Contain("short element reference");
    }

    [Fact]
    public void PlaywrightArgs_Validate_ValidRef_ShouldPass()
    {
        var args = new PlaywrightArgs { Action = "click", Ref = "e15" };
        var (isValid, error) = args.Validate();

        isValid.Should().BeTrue();
    }

    // --- ValidateNotPlaceholder ---

    [Fact]
    public void ValidateNotPlaceholder_Null_ShouldBeValid()
    {
        var (isValid, error) = PlaywrightArgs.ValidateNotPlaceholder(null, "url");

        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void ValidateNotPlaceholder_Empty_ShouldBeValid()
    {
        var (isValid, error) = PlaywrightArgs.ValidateNotPlaceholder("", "url");

        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateNotPlaceholder_ActualUrl_ShouldBeValid()
    {
        var (isValid, error) = PlaywrightArgs.ValidateNotPlaceholder("https://example.com", "url");

        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("url of the result")]
    [InlineData("the search button")]
    [InlineData("a button element")]
    [InlineData("from step 1")]
    [InlineData("e.g., https://example.com")]
    [InlineData("placeholder text")]
    [InlineData("insert your URL here")]
    [InlineData("your search query")]
    [InlineData("specify the URL")]
    public void ValidateNotPlaceholder_PlaceholderText_ShouldBeInvalid(string value)
    {
        var (isValid, error) = PlaywrightArgs.ValidateNotPlaceholder(value, "param");

        isValid.Should().BeFalse();
        error.Should().Contain("placeholder");
    }
}
