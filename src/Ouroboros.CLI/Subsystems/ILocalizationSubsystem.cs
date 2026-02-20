// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

/// <summary>
/// Manages culture/language handling: name lookups, voice selection, localized strings,
/// thought translation, and language directives for LLM prompts.
/// </summary>
public interface ILocalizationSubsystem : IAgentSubsystem
{
    /// <summary>Maps a culture code (e.g. "de-DE") to a human-readable language name.</summary>
    string GetLanguageName(string culture);

    /// <summary>Returns the default Azure TTS voice for a given culture code.</summary>
    string GetDefaultVoiceForCulture(string? culture);

    /// <summary>Returns the effective TTS voice, considering culture override.</summary>
    string GetEffectiveVoice();

    /// <summary>Returns a localized string for the given key, falling back to the key itself.</summary>
    string GetLocalizedString(string key);

    /// <summary>Returns a localized time-of-day label for the given hour (0-23).</summary>
    string GetLocalizedTimeOfDay(int hour);

    /// <summary>Returns an array of localized fallback greeting strings.</summary>
    string[] GetLocalizedFallbackGreetings(string timeOfDay);

    /// <summary>Translates a thought to the configured language if a non-English culture is set.</summary>
    Task<string> TranslateThoughtIfNeededAsync(string thought);

    /// <summary>Returns a language directive string for embedding in LLM prompts.</summary>
    string GetLanguageDirective();
}
