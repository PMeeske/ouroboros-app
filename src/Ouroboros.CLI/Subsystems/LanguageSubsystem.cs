// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Application.Configuration;
using Ouroboros.CLI.Services;
using Ouroboros.Providers;

/// <summary>
/// Language-detection agent submodule.
///
/// Uses <b>aya-expanse:8b</b> (Cohere's dedicated 23-language model, hosted on the
/// same Ollama cloud endpoint as the main chat model) to reliably identify the
/// language a user's input is written in, so Iaret can:
///   1. Include a LANGUAGE INSTRUCTION in her LLM system prompt.
///   2. Update the Azure TTS culture so <c>en-US-AvaMultilingualNeural</c> speaks
///      in the correct accent / phonetics.
///
/// Falls back to the heuristic <see cref="LanguageDetector"/> on timeout or error
/// (non-Latin scripts like Cyrillic / CJK / Arabic are caught by the heuristic
/// before the LLM is ever called, keeping latency near zero for those languages).
///
/// Exposed statically via <see cref="DetectStaticAsync"/> so the static
/// <c>ImmersiveMode</c> and <c>RoomMode</c> classes can call it without DI.
/// </summary>
public sealed class LanguageSubsystem : ILanguageSubsystem
{
    /// <summary>Default Ollama model for language detection.</summary>
    public const string DefaultModel = "aya-expanse:8b";

    private static readonly TimeSpan DetectionTimeout = TimeSpan.FromSeconds(2);

    private string _endpoint = DefaultEndpoints.Ollama;
    private string _model    = DefaultModel;

    // Static accessor — set on InitializeAsync so static callers work.
    private static LanguageSubsystem? _current;

    // ── IAgentSubsystem ───────────────────────────────────────────────────────

    public string Name          => "Language";
    public bool   IsInitialized { get; private set; }

    public Task InitializeAsync(SubsystemInitContext ctx)
    {
        _endpoint = ctx.Config.Endpoint ?? DefaultEndpoints.Ollama;
        _model    = ctx.Config.LanguageModel ?? DefaultModel;
        _current  = this;
        IsInitialized = true;
        ctx.Output.RecordInit("Language", true,
            $"model: {_model} @ {_endpoint} (fallback: heuristic)");
        return Task.CompletedTask;
    }

    // ── ILanguageSubsystem ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<LanguageDetector.DetectedLanguage> DetectAsync(
        string text, CancellationToken ct = default)
    {
        // Fast path: non-Latin scripts are detected by Unicode ranges — no LLM needed.
        var heuristic = LanguageDetector.Detect(text);
        if (heuristic.Culture != "en-US")
            return heuristic;

        if (string.IsNullOrWhiteSpace(text) || text.Length < 4)
            return heuristic;

        // LLM-based detection with a hard 2-second ceiling.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(DetectionTimeout);

        try
        {
            var settings = new ChatRuntimeSettings(Temperature: 0.0, MaxTokens: 32, TimeoutSeconds: 5, Stream: false);
            var llm = new OllamaCloudChatModel(_endpoint, "ollama", _model, settings);

            var prompt =
                "Identify the language of the text below. " +
                "Reply with ONLY the BCP-47 code and English language name separated by | " +
                "(e.g. de-DE|German, fr-FR|French, es-ES|Spanish, en-US|English). No other text.\n\n" +
                $"Text: {text}";

            var response = await llm
                .GenerateTextAsync(prompt, cts.Token)
                .ConfigureAwait(false);

            // Parse "de-DE|German"
            var pipe = response.Trim().IndexOf('|');
            if (pipe > 0)
            {
                var code = response[..pipe].Trim();
                var name = response[(pipe + 1)..].Trim().Split('\n', '\r')[0].Trim();
                // Validate minimal BCP-47: two-letter lang + hyphen + region
                if (code.Length >= 4 && code.Length <= 8 && code.Contains('-'))
                    return new LanguageDetector.DetectedLanguage(name, code);
            }
        }
        catch
        {
            // Timeout or model unavailable — silently fall back.
        }

        return heuristic;
    }

    // ── Static entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Detects language using the active <see cref="LanguageSubsystem"/> instance,
    /// or the heuristic <see cref="LanguageDetector"/> when no instance is registered.
    /// Called by the static <c>ImmersiveMode</c> and <c>RoomMode</c> classes.
    /// </summary>
    public static Task<LanguageDetector.DetectedLanguage> DetectStaticAsync(
        string text, CancellationToken ct = default)
        => _current?.DetectAsync(text, ct)
           ?? Task.FromResult(LanguageDetector.Detect(text));

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public ValueTask DisposeAsync()
    {
        if (_current == this) _current = null;
        return ValueTask.CompletedTask;
    }
}
