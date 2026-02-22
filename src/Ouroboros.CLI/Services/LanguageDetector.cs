// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Services;

/// <summary>
/// Lightweight language detector using heuristic stop-word scoring.
/// No external dependencies — works offline, zero latency.
///
/// Supports: English, German, French, Spanish, Italian, Dutch, Portuguese,
///           Russian, Japanese, Chinese, Korean, Arabic.
///
/// Accuracy: reliable for utterances of 3+ words. Short or ambiguous input returns English.
/// </summary>
public static class LanguageDetector
{
    /// <summary>A detected language with its BCP-47 culture code.</summary>
    public sealed record DetectedLanguage(string Language, string Culture);

    private static readonly DetectedLanguage English = new("English", "en-US");

    /// <summary>
    /// Detects the primary language of <paramref name="text"/>.
    /// Returns <c>English / en-US</c> when the signal is ambiguous or too short.
    /// </summary>
    public static DetectedLanguage Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
            return English;

        // ── Non-Latin scripts: highly reliable, checked first ────────────────

        if (text.Any(c => c >= '\u0400' && c <= '\u04FF'))
            return new("Russian", "ru-RU");

        if (text.Any(c => c >= '\u0600' && c <= '\u06FF'))
            return new("Arabic", "ar-SA");

        if (text.Any(c => c >= '\uAC00' && c <= '\uD7AF'))
            return new("Korean", "ko-KR");

        if (text.Any(c => (c >= '\u3040' && c <= '\u30FF') || (c >= '\u4E00' && c <= '\u9FFF')))
        {
            // Hiragana/Katakana → Japanese; pure CJK → Chinese
            bool hasKana = text.Any(c => c >= '\u3040' && c <= '\u30FF');
            return hasKana ? new("Japanese", "ja-JP") : new("Chinese", "zh-CN");
        }

        // ── Latin-script languages: stop-word scoring ────────────────────────

        var words = text.ToLowerInvariant()
            .Split([' ', ',', '.', '!', '?', '\n', '\r', '\t', ':', ';', '"', '\''],
                   StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0) return English;

        int en = Score(words, EnglishWords);
        int de = Score(words, GermanWords);
        int fr = Score(words, FrenchWords);
        int es = Score(words, SpanishWords);
        int it = Score(words, ItalianWords);
        int nl = Score(words, DutchWords);
        int pt = Score(words, PortugueseWords);

        // Require at least 2 non-English hits to avoid single-word false positives.
        // English needs only 1 hit (it wins all ties).
        int maxForeign = Math.Max(de, Math.Max(fr, Math.Max(es, Math.Max(it, Math.Max(nl, pt)))));

        if (maxForeign < 2) return English;

        // If English scored as well as the best foreign language, stay English.
        if (en >= maxForeign) return English;

        // Pick the highest-scoring foreign language; ties broken by order.
        if (de == maxForeign) return new("German",     "de-DE");
        if (fr == maxForeign) return new("French",     "fr-FR");
        if (es == maxForeign) return new("Spanish",    "es-ES");
        if (it == maxForeign) return new("Italian",    "it-IT");
        if (nl == maxForeign) return new("Dutch",      "nl-NL");
        if (pt == maxForeign) return new("Portuguese", "pt-PT");

        return English;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static int Score(string[] words, HashSet<string> vocab)
        => words.Count(vocab.Contains);

    // ── Stop-word vocabularies ────────────────────────────────────────────────

    private static readonly HashSet<string> EnglishWords = new(StringComparer.Ordinal)
    {
        // Articles / determiners
        "the", "a", "an", "this", "that", "these", "those",
        // Pronouns
        "i", "you", "he", "she", "it", "we", "they", "me", "him", "her", "us",
        "them", "my", "your", "his", "its", "our", "their",
        // Verbs
        "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "do", "does", "did",
        "will", "would", "could", "should", "may", "might", "can",
        "get", "got", "make", "just", "let", "know", "think", "want",
        // Conjunctions / prepositions
        "and", "or", "but", "not", "no", "so", "as", "if",
        "of", "on", "in", "to", "for", "with", "at", "by", "from",
        "up", "out", "into", "than", "then", "over", "also",
        // Common adverbs / adjectives
        "how", "what", "when", "where", "who", "why", "which",
        "all", "about", "more", "here", "there", "now", "very",
        "well", "even", "back", "still", "too", "both", "each",
        "same", "off", "such", "own", "few", "new", "good", "great",
    };

    private static readonly HashSet<string> GermanWords = new(StringComparer.Ordinal)
    {
        "ich", "du", "er", "sie", "es", "wir", "ihr", "ist", "bin", "bist",
        "sind", "war", "waren", "haben", "hat", "habe", "hast", "wird", "werden",
        "kann", "könnte", "muss", "musst", "nicht", "kein", "keine", "aber",
        "und", "oder", "dass", "wenn", "weil", "mit", "auf", "von", "zu",
        "in", "an", "die", "der", "das", "den", "dem", "ein", "eine", "einen",
        "einer", "einem", "wie", "was", "wer", "wo", "wann", "warum", "auch",
        "schon", "noch", "sehr", "mehr", "gut", "ja", "nein", "bitte", "danke",
        "hallo", "tschüss", "natürlich", "vielleicht", "immer", "jetzt", "hier",
        "dort", "so", "als", "bei", "nach", "über", "unter", "zwischen", "vor",
        "mich", "mir", "dich", "dir", "uns", "euch", "sich", "ihm", "ihr",
    };

    private static readonly HashSet<string> FrenchWords = new(StringComparer.Ordinal)
    {
        "je", "tu", "il", "elle", "nous", "vous", "ils", "elles", "est", "sont",
        "avoir", "être", "faire", "aller", "pouvoir", "vouloir", "savoir", "voir",
        "pas", "ne", "non", "oui", "mais", "et", "ou", "que", "qui", "quoi",
        "avec", "sur", "dans", "par", "pour", "de", "du", "la", "le", "les",
        "un", "une", "des", "ce", "cette", "ces", "mon", "ma", "mes", "ton",
        "bonjour", "merci", "bien", "très", "aussi", "encore", "déjà", "toujours",
        "comme", "plus", "moins", "peut", "fait", "dit", "suis", "serait",
        "moi", "toi", "lui", "leur", "ici", "là", "alors", "donc", "car",
        "quand", "comment", "pourquoi", "quel", "quelle", "tout", "tous",
    };

    private static readonly HashSet<string> SpanishWords = new(StringComparer.Ordinal)
    {
        "yo", "tú", "él", "ella", "nosotros", "vosotros", "ellos", "ellas",
        "es", "son", "estar", "ser", "tener", "hacer", "ir", "poder", "querer",
        "sí", "pero", "y", "que", "quien", "qué", "cómo", "cuándo",
        "con", "en", "del", "la", "el", "los", "las", "un", "una",
        "unos", "unas", "este", "esta", "estos", "estas", "mi", "tu", "su",
        "muy", "más", "también", "ya", "bien", "hola", "gracias", "siempre",
        "aquí", "allí", "cuando", "donde", "porque", "como", "todo", "todos",
        "hay", "tiene", "tienen",
    };

    private static readonly HashSet<string> ItalianWords = new(StringComparer.Ordinal)
    {
        "io", "tu", "lui", "lei", "noi", "voi", "loro", "è", "sono", "essere",
        "avere", "fare", "andare", "potere", "volere", "sapere", "vedere",
        "non", "sì", "ma", "che", "chi", "cosa", "come",
        "con", "su", "di", "la", "il", "lo", "le", "gli",
        "del", "della", "dei", "delle", "questo", "questa",
        "ciao", "grazie", "prego", "bene", "molto", "anche", "già", "sempre",
        "qui", "lì", "quando", "dove", "perché", "tutto", "tutti",
        "mi", "ti", "si", "ci", "vi", "ne", "ha", "hanno", "ho", "hai",
    };

    private static readonly HashSet<string> DutchWords = new(StringComparer.Ordinal)
    {
        "ik", "jij", "hij", "zij", "wij", "jullie", "zijn", "was", "waren",
        "hebben", "heeft", "heb", "worden", "kan", "moet", "zal", "zou",
        "niet", "geen", "maar", "en", "of", "dat", "die", "wat", "wie", "waar",
        "met", "op", "van", "te", "aan", "de", "het", "een", "ook", "nog",
        "hallo", "dank", "goed", "heel", "altijd", "wel", "al", "zoals",
        "mij", "jou", "hem", "haar", "ons", "hun", "ze", "dit", "deze",
        "hier", "daar", "wanneer", "waarom", "hoe", "alles", "iets",
    };

    private static readonly HashSet<string> PortugueseWords = new(StringComparer.Ordinal)
    {
        // Kept only words that are distinctly Portuguese and rarely appear in English text.
        "eu", "tu", "ele", "ela", "nós", "vós", "eles", "elas", "é", "são",
        "ser", "estar", "ter", "fazer", "ir", "poder", "querer", "saber", "ver",
        "não", "sim", "mas", "que", "quem", "como", "quando",
        "com", "em", "da", "do", "um", "uma",
        "este", "esta", "isso", "aqui", "já", "bem", "muito", "mais", "também",
        "olá", "obrigado", "obrigada", "sempre", "tudo", "nada", "todo",
        "lhe", "lhes", "meu", "minha", "seu", "sua",
        // Removed: "a", "o", "as", "os", "de", "e", "ou", "me", "te", "se", "nos"
        // — all appear frequently in English and caused Portuguese false positives.
    };
}
