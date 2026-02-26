using Ouroboros.Easy.Localization;

namespace Ouroboros.Tests.Localization;

[Trait("Category", "Unit")]
public sealed class MultiLanguageSupportTests
{
    public MultiLanguageSupportTests()
    {
        // Reset to English before each test to avoid cross-test pollution
        MultiLanguageSupport.CurrentLanguage = "en";
    }

    [Fact]
    public void CurrentLanguage_Default_IsEnglish()
    {
        // Assert
        MultiLanguageSupport.CurrentLanguage.Should().Be("en");
    }

    [Fact]
    public void CurrentLanguage_SetToGerman_ReturnsGerman()
    {
        // Act
        MultiLanguageSupport.CurrentLanguage = "de";

        // Assert
        MultiLanguageSupport.CurrentLanguage.Should().Be("de");
    }

    [Fact]
    public void CurrentLanguage_SetToUnsupported_FallsBackToEnglish()
    {
        // Act
        MultiLanguageSupport.CurrentLanguage = "xx";

        // Assert
        MultiLanguageSupport.CurrentLanguage.Should().Be("en");
    }

    [Fact]
    public void Get_EnglishWelcomeMessage_ReturnsExpected()
    {
        // Arrange
        MultiLanguageSupport.CurrentLanguage = "en";

        // Act
        var message = MultiLanguageSupport.Get("WelcomeMessage");

        // Assert
        message.Should().Be("Welcome to Ouroboros!");
    }

    [Fact]
    public void Get_GermanWelcomeMessage_ReturnsGermanText()
    {
        // Arrange
        MultiLanguageSupport.CurrentLanguage = "de";

        // Act
        var message = MultiLanguageSupport.Get("WelcomeMessage");

        // Assert
        message.Should().Be("Willkommen bei Ouroboros!");
    }

    [Fact]
    public void Get_FrenchWelcomeMessage_ReturnsFrenchText()
    {
        // Arrange
        MultiLanguageSupport.CurrentLanguage = "fr";

        // Act
        var message = MultiLanguageSupport.Get("WelcomeMessage");

        // Assert
        message.Should().Contain("Bienvenue");
    }

    [Fact]
    public void Get_WithFormatArgs_FormatsCorrectly()
    {
        // Arrange
        MultiLanguageSupport.CurrentLanguage = "en";

        // Act
        var message = MultiLanguageSupport.Get("PipelineFailed", "timeout occurred");

        // Assert
        message.Should().Be("Pipeline execution failed: timeout occurred");
    }

    [Fact]
    public void Get_UnknownKey_ReturnsKeyItself()
    {
        // Act
        var message = MultiLanguageSupport.Get("NonExistentProperty");

        // Assert
        message.Should().Be("NonExistentProperty");
    }

    [Fact]
    public void SupportedLanguages_ContainsExpectedLanguages()
    {
        // Act
        var languages = MultiLanguageSupport.SupportedLanguages.ToList();

        // Assert
        languages.Should().Contain("en");
        languages.Should().Contain("de");
        languages.Should().Contain("fr");
    }

    [Fact]
    public void IsLanguageSupported_English_ReturnsTrue()
    {
        // Assert
        MultiLanguageSupport.IsLanguageSupported("en").Should().BeTrue();
    }

    [Fact]
    public void IsLanguageSupported_German_ReturnsTrue()
    {
        // Assert
        MultiLanguageSupport.IsLanguageSupported("de").Should().BeTrue();
    }

    [Fact]
    public void IsLanguageSupported_Unsupported_ReturnsFalse()
    {
        // Assert
        MultiLanguageSupport.IsLanguageSupported("xx").Should().BeFalse();
    }

    [Fact]
    public void Get_GermanErrorOccurred_FormatsCorrectly()
    {
        // Arrange
        MultiLanguageSupport.CurrentLanguage = "de";

        // Act
        var message = MultiLanguageSupport.Get("ErrorOccurred", "Verbindungsfehler");

        // Assert
        message.Should().Contain("Fehler");
        message.Should().Contain("Verbindungsfehler");
    }
}
