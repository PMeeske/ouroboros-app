using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

namespace Ouroboros.Tests.CLI.Infrastructure;

[Trait("Category", "Unit")]
public class OuroborosThemeTests
{
    [Fact]
    public void Colors_AreNotNull()
    {
        OuroborosTheme.Purple.Should().NotBeNull();
        OuroborosTheme.DeepPurple.Should().NotBeNull();
        OuroborosTheme.Violet.Should().NotBeNull();
        OuroborosTheme.Gold.Should().NotBeNull();
        OuroborosTheme.SoftGold.Should().NotBeNull();
    }

    [Fact]
    public void SemanticColors_AreNotNull()
    {
        OuroborosTheme.Success.Should().NotBeNull();
        OuroborosTheme.Warning.Should().NotBeNull();
        OuroborosTheme.Error.Should().NotBeNull();
        OuroborosTheme.Muted.Should().NotBeNull();
    }

    [Fact]
    public void Styles_AreNotNull()
    {
        OuroborosTheme.HeaderStyle.Should().NotBeNull();
        OuroborosTheme.BannerStyle.Should().NotBeNull();
        OuroborosTheme.BannerAccent.Should().NotBeNull();
        OuroborosTheme.AccentStyle.Should().NotBeNull();
        OuroborosTheme.GoldStyle.Should().NotBeNull();
        OuroborosTheme.MutedStyle.Should().NotBeNull();
        OuroborosTheme.BorderStyle.Should().NotBeNull();
        OuroborosTheme.PromptStyle.Should().NotBeNull();
        OuroborosTheme.SuccessStyle.Should().NotBeNull();
        OuroborosTheme.ErrorStyle.Should().NotBeNull();
        OuroborosTheme.WarningStyle.Should().NotBeNull();
    }

    [Fact]
    public void Accent_ReturnsMarkupString()
    {
        var result = OuroborosTheme.Accent("test");

        result.Should().Contain("test");
        result.Should().Contain("rgb(148,103,189)");
    }

    [Fact]
    public void GoldText_ReturnsMarkupString()
    {
        var result = OuroborosTheme.GoldText("gold");

        result.Should().Contain("gold");
        result.Should().Contain("rgb(255,200,50)");
    }

    [Fact]
    public void Header_ReturnsMarkupString()
    {
        var result = OuroborosTheme.Header("header");

        result.Should().Contain("header");
    }

    [Fact]
    public void Ok_ReturnsGreenMarkup()
    {
        var result = OuroborosTheme.Ok("success");

        result.Should().Contain("success");
        result.Should().Contain("[green]");
    }

    [Fact]
    public void Err_ReturnsRedMarkup()
    {
        var result = OuroborosTheme.Err("error");

        result.Should().Contain("error");
        result.Should().Contain("[red]");
    }

    [Fact]
    public void Warn_ReturnsYellowMarkup()
    {
        var result = OuroborosTheme.Warn("warning");

        result.Should().Contain("warning");
        result.Should().Contain("[yellow]");
    }

    [Fact]
    public void Dim_ReturnsGreyMarkup()
    {
        var result = OuroborosTheme.Dim("muted");

        result.Should().Contain("muted");
        result.Should().Contain("[grey]");
    }

    [Fact]
    public void Accent_EscapesSpecialCharacters()
    {
        var result = OuroborosTheme.Accent("[special]");

        // Should escape brackets for Spectre
        result.Should().Contain("[[special]]");
    }

    [Fact]
    public void ThemedTable_CreatesTableWithColumns()
    {
        var table = OuroborosTheme.ThemedTable("Col1", "Col2", "Col3");

        table.Should().NotBeNull();
        table.Columns.Should().HaveCount(3);
    }
}
