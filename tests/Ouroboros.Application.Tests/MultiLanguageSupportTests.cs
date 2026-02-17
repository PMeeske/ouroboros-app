using Ouroboros.Easy.Localization;

namespace Ouroboros.Tests.Easy;

[Trait("Category", "Unit")]
[Trait("Area", "Localization")]
public class MultiLanguageSupportTests
{
    [Fact]
    public void CurrentLanguage_ShouldSupportGerman()
    {
        MultiLanguageSupport.CurrentLanguage = "de";
        MultiLanguageSupport.Get("WelcomeMessage").Should().Contain("Willkommen");
    }

    [Fact]
    public void IsLanguageSupported_ShouldReturnTrueForSupportedLanguages()
    {
        bool isSupported = MultiLanguageSupport.IsLanguageSupported("de");
        isSupported.Should().BeTrue();
    }
}