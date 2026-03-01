using Ouroboros.CLI.Services;

namespace Ouroboros.Tests.CLI.Services;

[Trait("Category", "Unit")]
public class LanguageDetectorTests
{
    [Fact]
    public void Detect_NullOrEmpty_ReturnsEnglish()
    {
        LanguageDetector.Detect("").Language.Should().Be("English");
        LanguageDetector.Detect("  ").Language.Should().Be("English");
    }

    [Fact]
    public void Detect_ShortInput_ReturnsEnglish()
    {
        var result = LanguageDetector.Detect("hi");
        result.Language.Should().Be("English");
        result.Culture.Should().Be("en-US");
    }

    [Fact]
    public void Detect_EnglishText_ReturnsEnglish()
    {
        var result = LanguageDetector.Detect("The quick brown fox jumps over the lazy dog");
        result.Language.Should().Be("English");
        result.Culture.Should().Be("en-US");
    }

    [Fact]
    public void Detect_GermanText_ReturnsGerman()
    {
        var result = LanguageDetector.Detect("Ich bin nicht sicher, was ich machen soll");
        result.Language.Should().Be("German");
        result.Culture.Should().Be("de-DE");
    }

    [Fact]
    public void Detect_FrenchText_ReturnsFrench()
    {
        var result = LanguageDetector.Detect("Je ne suis pas sûr de ce que je dois faire");
        result.Language.Should().Be("French");
        result.Culture.Should().Be("fr-FR");
    }

    [Fact]
    public void Detect_SpanishText_ReturnsSpanish()
    {
        var result = LanguageDetector.Detect("No estoy seguro de qué hacer, pero quiero saber más");
        result.Language.Should().Be("Spanish");
        result.Culture.Should().Be("es-ES");
    }

    [Fact]
    public void Detect_RussianText_ReturnsRussian()
    {
        var result = LanguageDetector.Detect("Привет, как у тебя дела?");
        result.Language.Should().Be("Russian");
        result.Culture.Should().Be("ru-RU");
    }

    [Fact]
    public void Detect_ArabicText_ReturnsArabic()
    {
        var result = LanguageDetector.Detect("مرحبا كيف حالك");
        result.Language.Should().Be("Arabic");
        result.Culture.Should().Be("ar-SA");
    }

    [Fact]
    public void Detect_KoreanText_ReturnsKorean()
    {
        var result = LanguageDetector.Detect("안녕하세요 어떻게 지내세요");
        result.Language.Should().Be("Korean");
        result.Culture.Should().Be("ko-KR");
    }

    [Fact]
    public void Detect_JapaneseText_ReturnsJapanese()
    {
        var result = LanguageDetector.Detect("こんにちは、お元気ですか");
        result.Language.Should().Be("Japanese");
        result.Culture.Should().Be("ja-JP");
    }

    [Fact]
    public void Detect_ChineseText_ReturnsChinese()
    {
        var result = LanguageDetector.Detect("你好世界今天天气好");
        result.Language.Should().Be("Chinese");
        result.Culture.Should().Be("zh-CN");
    }

    [Fact]
    public void DetectedLanguage_Record_HasCorrectProperties()
    {
        var lang = new LanguageDetector.DetectedLanguage("English", "en-US");

        lang.Language.Should().Be("English");
        lang.Culture.Should().Be("en-US");
    }

    [Fact]
    public void Detect_ItalianText_ReturnsItalian()
    {
        var result = LanguageDetector.Detect("Io non sono sicuro di cosa fare, ma vorrei sapere");
        result.Language.Should().Be("Italian");
        result.Culture.Should().Be("it-IT");
    }

    [Fact]
    public void Detect_DutchText_ReturnsDutch()
    {
        var result = LanguageDetector.Detect("Ik weet niet wat ik moet doen, maar ik wil het weten");
        result.Language.Should().Be("Dutch");
        result.Culture.Should().Be("nl-NL");
    }

    [Fact]
    public void Detect_AmbiguousSingleWord_ReturnsEnglish()
    {
        var result = LanguageDetector.Detect("hello world");
        result.Language.Should().Be("English");
    }
}
