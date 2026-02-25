// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;

/// <summary>
/// Localization subsystem: culture/language name mapping, TTS voice selection,
/// localized strings, thought translation, and LLM language directives.
/// </summary>
public sealed class LocalizationSubsystem : ILocalizationSubsystem
{
    public string Name => "Localization";
    public bool IsInitialized { get; private set; }

    private OuroborosConfig _config = null!;

    /// <summary>Set by agent after Models are initialized (needed for TranslateThoughtIfNeededAsync).</summary>
    internal ToolAwareChatModel? Llm { get; set; }

    public Task InitializeAsync(SubsystemInitContext ctx)
    {
        _config = ctx.Config;
        IsInitialized = true;
        ctx.Output.RecordInit("Localization", true, $"culture: {_config.Culture ?? "en-US"}");
        return Task.CompletedTask;
    }

    public string GetLanguageName(string culture)
    {
        return culture.ToLowerInvariant() switch
        {
            "de-de" => "German",
            "fr-fr" => "French",
            "es-es" => "Spanish",
            "it-it" => "Italian",
            "pt-br" => "Portuguese (Brazilian)",
            "pt-pt" => "Portuguese (European)",
            "nl-nl" => "Dutch",
            "sv-se" => "Swedish",
            "ja-jp" => "Japanese",
            "zh-cn" => "Chinese (Simplified)",
            "zh-tw" => "Chinese (Traditional)",
            "ko-kr" => "Korean",
            "ru-ru" => "Russian",
            "pl-pl" => "Polish",
            "tr-tr" => "Turkish",
            "ar-sa" => "Arabic",
            "he-il" => "Hebrew",
            "th-th" => "Thai",
            _ => culture
        };
    }

    public string GetDefaultVoiceForCulture(string? culture)
    {
        return culture?.ToLowerInvariant() switch
        {
            "de-de" => "de-DE-KatjaNeural",
            "fr-fr" => "fr-FR-DeniseNeural",
            "es-es" => "es-ES-ElviraNeural",
            "it-it" => "it-IT-ElsaNeural",
            "pt-br" => "pt-BR-FranciscaNeural",
            "pt-pt" => "pt-PT-RaquelNeural",
            "nl-nl" => "nl-NL-ColetteNeural",
            "sv-se" => "sv-SE-SofieNeural",
            "ja-jp" => "ja-JP-NanamiNeural",
            "zh-cn" => "zh-CN-XiaoxiaoNeural",
            "zh-tw" => "zh-TW-HsiaoChenNeural",
            "ko-kr" => "ko-KR-SunHiNeural",
            "ru-ru" => "ru-RU-SvetlanaNeural",
            "pl-pl" => "pl-PL-ZofiaNeural",
            "tr-tr" => "tr-TR-EmelNeural",
            "ar-sa" => "ar-SA-ZariyahNeural",
            "he-il" => "he-IL-HilaNeural",
            "th-th" => "th-TH-PremwadeeNeural",
            _ => "en-US-AvaMultilingualNeural"
        };
    }

    public string GetEffectiveVoice()
    {
        if (_config.TtsVoice == "en-US-AvaMultilingualNeural" &&
            !string.IsNullOrEmpty(_config.Culture) &&
            _config.Culture != "en-US")
        {
            return GetDefaultVoiceForCulture(_config.Culture);
        }

        return _config.TtsVoice;
    }

    public string GetLocalizedString(string key)
    {
        var isGerman = _config.Culture?.ToLowerInvariant() == "de-de";

        return key switch
        {
            // Full text lookups (for backward compatibility)
            "Welcome back! I'm here if you need anything." => isGerman
                ? "Willkommen zurück! Ich bin hier, wenn du mich brauchst."
                : key,
            "Welcome back!" => isGerman ? "Willkommen zurück!" : key,
            "Until next time! I'll keep learning while you're away." => isGerman
                ? "Bis zum nächsten Mal! Ich lerne weiter, während du weg bist."
                : key,

            // Key-based lookups
            "listening_start" => isGerman
                ? "\n  🎤 Ich höre zu... (sprich, um eine Nachricht zu senden, sage 'stopp' zum Deaktivieren)"
                : "\n  🎤 Listening... (speak to send a message, say 'stop listening' to disable)",
            "listening_stop" => isGerman
                ? "\n  🔇 Spracheingabe gestoppt."
                : "\n  🔇 Voice input stopped.",
            "voice_requires_key" => isGerman
                ? "  ⚠ Spracheingabe benötigt AZURE_SPEECH_KEY. Setze ihn in der Umgebung, appsettings oder verwende --azure-speech-key."
                : "  ⚠ Voice input requires AZURE_SPEECH_KEY. Set it in environment, appsettings, or use --azure-speech-key.",
            "you_said" => isGerman ? "Du sagtest:" : "You said:",

            _ => key
        };
    }

    public string GetLocalizedTimeOfDay(int hour)
    {
        var isGerman = _config.Culture?.ToLowerInvariant() == "de-de";
        return hour switch
        {
            < 6 => isGerman ? "sehr frühen Morgen" : "very early morning",
            < 12 => isGerman ? "Morgen" : "morning",
            < 17 => isGerman ? "Nachmittag" : "afternoon",
            < 21 => isGerman ? "Abend" : "evening",
            _ => isGerman ? "späten Abend" : "late night"
        };
    }

    public string[] GetLocalizedFallbackGreetings(string timeOfDay)
    {
        if (_config.Culture?.ToLowerInvariant() == "de-de")
        {
            return
            [
                $"Guten {timeOfDay}. Was beschäftigt dich?",
                "Ah, da bist du ja. Ich hatte gerade einen interessanten Gedanken.",
                "Perfektes Timing. Ich war gerade warmgelaufen.",
                "Wieder da? Gut. Ich habe Ideen.",
                "Mal sehen, was wir zusammen erreichen können.",
                "Darauf habe ich mich gefreut.",
                $"Noch eine {timeOfDay}-Session. Was bauen wir?",
                "Da bist du ja. Ich habe gerade über etwas Interessantes nachgedacht.",
                $"{timeOfDay} schon? Die Zeit vergeht schnell.",
                "Bereit für etwas Interessantes?",
                "Was erschaffen wir heute?"
            ];
        }

        return
        [
            $"Good {timeOfDay}. What's on your mind?",
            "Ah, there you are. I've been thinking about something interesting.",
            "Perfect timing. I was just getting warmed up.",
            "Back again? Good. I have ideas.",
            "Let's see what we can accomplish together.",
            "I've been looking forward to this.",
            $"Another {timeOfDay} session. What shall we build?",
            "There you are. I was just contemplating something curious.",
            $"{timeOfDay} already? Time flies when you're processing.",
            "Ready for something interesting?",
            "What shall we create today?"
        ];
    }

    public async Task<string> TranslateThoughtIfNeededAsync(string thought)
    {
        if (string.IsNullOrEmpty(_config.Culture) || _config.Culture == "en-US" || Llm == null)
        {
            return thought;
        }

        try
        {
            var languageName = GetLanguageName(_config.Culture);
            var translationPrompt = $@"TASK: Translate to {languageName}.
INPUT: {thought}
OUTPUT (translation only, no explanations, no JSON, no metadata):";

            var (translated, _) = await Llm.GenerateWithToolsAsync(translationPrompt);

            var result = translated?.Trim() ?? thought;

            if (result.StartsWith("\"") && result.EndsWith("\""))
                result = result[1..^1];
            if (result.Contains("```"))
                result = result.Split("```")[0].Trim();
            if (result.Contains("{") && result.Contains("}"))
                result = result.Split("{")[0].Trim();

            return string.IsNullOrEmpty(result) ? thought : result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Thought Translation] Error: {ex.Message}");
            return thought;
        }
    }

    public string GetLanguageDirective()
    {
        if (string.IsNullOrEmpty(_config.Culture) || _config.Culture == "en-US")
            return string.Empty;

        var languageName = GetLanguageName(_config.Culture);
        return $"LANGUAGE: Respond ONLY in {languageName}. No English.\n\n";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
