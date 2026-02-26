using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Subsystems;

namespace Ouroboros.Tests.CLI.Subsystems;

[Trait("Category", "Unit")]
public class LocalizationSubsystemTests
{
    private static LocalizationSubsystem CreateInitialized(string? culture = null)
    {
        var sub = new LocalizationSubsystem();
        var config = new OuroborosConfig() with { Culture = culture };
        var ctx = new SubsystemInitContext
        {
            Config = config,
            Output = new ConsoleOutput(OutputVerbosity.Quiet)
        };
        sub.InitializeAsync(ctx).GetAwaiter().GetResult();
        return sub;
    }

    [Fact]
    public void Name_IsLocalization()
    {
        var sub = new LocalizationSubsystem();
        sub.Name.Should().Be("Localization");
    }

    [Fact]
    public void IsInitialized_InitiallyFalse()
    {
        var sub = new LocalizationSubsystem();
        sub.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_SetsIsInitialized()
    {
        var sub = new LocalizationSubsystem();
        var config = new OuroborosConfig();
        var ctx = new SubsystemInitContext
        {
            Config = config,
            Output = new ConsoleOutput(OutputVerbosity.Quiet)
        };

        await sub.InitializeAsync(ctx);

        sub.IsInitialized.Should().BeTrue();
    }

    [Theory]
    [InlineData("de-DE", "German")]
    [InlineData("fr-FR", "French")]
    [InlineData("es-ES", "Spanish")]
    [InlineData("ja-JP", "Japanese")]
    [InlineData("zh-CN", "Chinese (Simplified)")]
    [InlineData("ko-KR", "Korean")]
    [InlineData("ru-RU", "Russian")]
    [InlineData("ar-SA", "Arabic")]
    public void GetLanguageName_KnownCultures_ReturnsLanguageName(string culture, string expected)
    {
        var sub = CreateInitialized();

        sub.GetLanguageName(culture).Should().Be(expected);
    }

    [Fact]
    public void GetLanguageName_UnknownCulture_ReturnsCultureCode()
    {
        var sub = CreateInitialized();

        sub.GetLanguageName("xx-YY").Should().Be("xx-yy");
    }

    [Theory]
    [InlineData("de-DE", "de-DE-KatjaNeural")]
    [InlineData("fr-FR", "fr-FR-DeniseNeural")]
    [InlineData("ja-JP", "ja-JP-NanamiNeural")]
    [InlineData(null, "en-US-AvaMultilingualNeural")]
    public void GetDefaultVoiceForCulture_ReturnsExpected(string? culture, string expected)
    {
        var sub = CreateInitialized();

        sub.GetDefaultVoiceForCulture(culture).Should().Be(expected);
    }

    [Fact]
    public void GetEffectiveVoice_DefaultCulture_ReturnsDefaultVoice()
    {
        var sub = CreateInitialized();

        sub.GetEffectiveVoice().Should().Be("en-US-AvaMultilingualNeural");
    }

    [Fact]
    public void GetEffectiveVoice_GermanCulture_ReturnsGermanVoice()
    {
        var sub = CreateInitialized("de-DE");

        sub.GetEffectiveVoice().Should().Be("de-DE-KatjaNeural");
    }

    [Fact]
    public void GetLocalizedString_EnglishCulture_ReturnsOriginal()
    {
        var sub = CreateInitialized("en-US");

        sub.GetLocalizedString("Welcome back!").Should().Be("Welcome back!");
    }

    [Fact]
    public void GetLocalizedString_GermanCulture_ReturnsTranslation()
    {
        var sub = CreateInitialized("de-DE");

        sub.GetLocalizedString("Welcome back!").Should().Be("Willkommen zurück!");
    }

    [Fact]
    public void GetLocalizedString_UnknownKey_ReturnsKey()
    {
        var sub = CreateInitialized();

        sub.GetLocalizedString("unknown_key").Should().Be("unknown_key");
    }

    [Theory]
    [InlineData(5, "very early morning")]
    [InlineData(8, "morning")]
    [InlineData(14, "afternoon")]
    [InlineData(19, "evening")]
    [InlineData(22, "late night")]
    public void GetLocalizedTimeOfDay_EnglishCulture(int hour, string expected)
    {
        var sub = CreateInitialized("en-US");

        sub.GetLocalizedTimeOfDay(hour).Should().Be(expected);
    }

    [Fact]
    public void GetLocalizedFallbackGreetings_ReturnsNonEmpty()
    {
        var sub = CreateInitialized();

        var greetings = sub.GetLocalizedFallbackGreetings("morning");

        greetings.Should().NotBeEmpty();
        greetings.Should().AllSatisfy(g => g.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public void GetLocalizedFallbackGreetings_GermanCulture_ReturnsGerman()
    {
        var sub = CreateInitialized("de-DE");

        var greetings = sub.GetLocalizedFallbackGreetings("Morgen");

        greetings.Should().NotBeEmpty();
    }

    [Fact]
    public void GetLanguageDirective_EnglishCulture_ReturnsEmpty()
    {
        var sub = CreateInitialized("en-US");

        sub.GetLanguageDirective().Should().BeEmpty();
    }

    [Fact]
    public void GetLanguageDirective_NullCulture_ReturnsEmpty()
    {
        var sub = CreateInitialized(null);

        sub.GetLanguageDirective().Should().BeEmpty();
    }

    [Fact]
    public void GetLanguageDirective_GermanCulture_ContainsGerman()
    {
        var sub = CreateInitialized("de-DE");

        var directive = sub.GetLanguageDirective();

        directive.Should().Contain("German");
        directive.Should().Contain("LANGUAGE");
    }

    [Fact]
    public async Task TranslateThoughtIfNeededAsync_NullCulture_ReturnsOriginal()
    {
        var sub = CreateInitialized(null);

        var result = await sub.TranslateThoughtIfNeededAsync("Hello");

        result.Should().Be("Hello");
    }

    [Fact]
    public async Task TranslateThoughtIfNeededAsync_EnglishCulture_ReturnsOriginal()
    {
        var sub = CreateInitialized("en-US");

        var result = await sub.TranslateThoughtIfNeededAsync("Hello");

        result.Should().Be("Hello");
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        var sub = new LocalizationSubsystem();

        var action = async () => await sub.DisposeAsync();

        await action.Should().NotThrowAsync();
    }
}
