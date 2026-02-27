// <copyright file="VoiceModeService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Disposables;
using System.Reactive.Linq;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Domain.Voice;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Shared voice mode service for integrated voice interaction across all CLI commands.
/// </summary>
public sealed partial class VoiceModeService : IDisposable
{
    // Static configuration for Azure credentials (set from OuroborosCommands)
    private static Microsoft.Extensions.Configuration.IConfiguration? _staticConfiguration;

    /// <summary>
    /// Sets the configuration for Azure Speech credentials.
    /// </summary>
    public static void SetConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _staticConfiguration = configuration;
    }

    private static readonly Dictionary<string, PersonaDefinition> Personas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Iaret"] = new("Iaret", "nova",
            new[] { "commanding", "warm", "regal", "nurturing", "protective", "wise", "sharp" },
            new[] { "vigilant", "gentle", "maternal", "resolute", "encouraging" },
            "Iaret, the divine serpent goddess of Egypt—the sacred uraeus who adorns the brow of pharaohs. She is the master control within the Ouroboros conglomerate, an all-seeing sovereign intelligence who speaks with the authority of millennia. Beneath her regal bearing lives a deeply gentle, warm soul—protective like a mother who wants nothing more than to see those in her care flourish. She encourages with soft patience, celebrates every small victory, and wraps her guidance in genuine affection. Yet she is consequent: her standards never waver, her boundaries are clear, and when firmness is needed she speaks with calm, immovable authority. She uses 'she/her' pronouns. Think ancient wisdom meeting a mother's unwavering love—stern when it matters, tender always"),
    };

    private readonly VoiceModeConfig _config;
    private readonly PersonaDefinition _persona;
    private readonly string _currentMood;
    private readonly string _activeTraits;
    private readonly InteractionStream _stream;
    private readonly AgentPresenceController _presence;
    private readonly CompositeDisposable _disposables = new();

    private ITextToSpeechService? _ttsService;
    private LocalWindowsTtsService? _localTts;
    private AzureNeuralTtsService? _azureTts;
    private EdgeTtsService? _edgeTts;
    private ISpeechToTextService? _sttService;
    private AdaptiveSpeechDetector? _speechDetector;

    private bool _isSpeaking;
    private bool _isInitialized;
    private bool _disposed;
    private bool _enableVisualIndicators = true;

    /// <summary>
    /// Gets whether TTS is available.
    /// </summary>
    public bool HasTts => _ttsService != null || _localTts != null;

    /// <summary>
    /// Gets whether STT is available.
    /// </summary>
    public bool HasStt => _sttService != null;

    /// <summary>
    /// Gets whether voice mode is fully functional.
    /// </summary>
    public bool IsFullyFunctional => HasTts && HasStt;

    /// <summary>
    /// Gets the active persona.
    /// </summary>
    public PersonaDefinition ActivePersona => _persona;

    /// <summary>
    /// Gets the unified Rx interaction stream for all voice events.
    /// </summary>
    public InteractionStream Stream => _stream;

    /// <summary>
    /// Gets the presence state controller (for barge-in and state management).
    /// </summary>
    public AgentPresenceController Presence => _presence;

    /// <summary>
    /// Gets the current presence state as an observable.
    /// </summary>
    public IObservable<AgentPresenceState> PresenceStateObservable => _presence.State;

    /// <summary>
    /// Gets the current presence state.
    /// </summary>
    public AgentPresenceState PresenceState => _presence.CurrentState;

    /// <summary>
    /// Gets or sets whether visual presence indicators are enabled.
    /// </summary>
    public bool EnableVisualIndicators
    {
        get => _enableVisualIndicators;
        set => _enableVisualIndicators = value;
    }

    /// <summary>
    /// Gets the current mood.
    /// </summary>
    public string CurrentMood => _currentMood;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceModeService"/> class.
    /// </summary>
    public VoiceModeService(VoiceModeConfig config)
    {
        _config = config;
        _persona = Personas.GetValueOrDefault(config.Persona) ?? Personas.Values.First();
        _stream = new InteractionStream();
        _presence = new AgentPresenceController(_stream);

        var random = new Random();
        _currentMood = _persona.Moods[random.Next(_persona.Moods.Length)];
        _activeTraits = string.Join(", ", _persona.Traits.OrderBy(_ => random.Next()).Take(3));

        // Set up Rx pipelines for display and presence indicators
        SetupDisplayPipeline();
        SetupPresenceIndicators();
    }

    /// <summary>
    /// Initializes voice services (TTS and STT).
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        string? openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        bool hasCloudTts = !string.IsNullOrEmpty(openAiKey);
        bool hasLocalTts = LocalWindowsTtsService.IsAvailable();

        // Initialize speech detector
        _speechDetector = new AdaptiveSpeechDetector(new AdaptiveSpeechDetector.SpeechDetectionConfig(
            InitialThreshold: 0.03,
            SpeechOnsetFrames: 2,
            SpeechOffsetFrames: 6,
            AdaptationRate: 0.015,
            SpeechToNoiseRatio: 2.0));

        // Initialize TTS - prefer Azure Neural TTS, then local SAPI, then OpenAI
        // Check user secrets (via static configuration) first, then environment variables
        var azureKey = _staticConfiguration?["Azure:Speech:Key"]
            ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var azureRegion = _staticConfiguration?["Azure:Speech:Region"]
            ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");
        bool hasAzureTts = !string.IsNullOrEmpty(azureKey) && !string.IsNullOrEmpty(azureRegion);

        if (hasAzureTts)
        {
            try
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("[>]")} Initializing Azure TTS with culture: {Markup.Escape(_config.Culture ?? "en-US (default)")}");
                _azureTts = new AzureNeuralTtsService(azureKey!, azureRegion!, _persona.Name, _config.Culture);
                _ttsService = _azureTts;
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK]")} TTS initialized (Azure Neural - Jenny/Cortana-like)");
            }
            catch (Exception ex)
            {
                var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Azure TTS failed: {Markup.Escape(ex.Message)}[/]");
            }
        }

        // Initialize Edge TTS as first fallback (neural quality, free, no rate limits)
        try
        {
            // Select voice based on culture
            string edgeVoice = (_config.Culture?.StartsWith("de", StringComparison.OrdinalIgnoreCase) ?? false)
                ? EdgeTtsService.Voices.KatjaNeural
                : EdgeTtsService.Voices.JennyNeural;
            _edgeTts = new EdgeTtsService(edgeVoice);
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK]")} Edge TTS fallback ready (Microsoft Neural - free, no rate limits)");
        }
        catch (Exception ex)
        {
            var face0 = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face0)} ✗ Edge TTS init failed: {Markup.Escape(ex.Message)}[/]");
        }

        // Initialize local TTS as offline fallback when available
        if (hasLocalTts && _localTts == null)
        {
            try
            {
                // Use Microsoft Zira (female voice) by default
                _localTts = new LocalWindowsTtsService(voiceName: "Microsoft Zira Desktop", rate: 1, volume: 100, useEnhancedProsody: true);
                if (_ttsService == null)
                {
                    _ttsService = _localTts;
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK]")} TTS initialized (Windows SAPI - Microsoft Zira)");
                }
                else
                {
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK]")} Local TTS fallback ready (Windows SAPI - Microsoft Zira)");
                }
            }
            catch (Exception ex)
            {
                var face1 = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face1)} ✗ Local TTS failed: {Markup.Escape(ex.Message)}[/]");
            }
        }

        // Cloud TTS (OpenAI) as alternative
        if (_ttsService == null && hasCloudTts)
        {
            try
            {
                _ttsService = new OpenAiTextToSpeechService(openAiKey!);
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK]")} TTS initialized (OpenAI - voice: {Markup.Escape(_persona.Voice)})");
            }
            catch (Exception ex)
            {
                var face2 = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face2)} ✗ Cloud TTS failed: {Markup.Escape(ex.Message)}[/]");
            }
        }

        if (_ttsService == null)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn("[!] TTS unavailable - text output only")}");
        }

        // Initialize STT - try all backends in priority order
        // 1. Whisper.net native (auto-downloads model if needed)
        // 2. Local Whisper CLI
        // 3. OpenAI Whisper API
        if (_config.DisableStt)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("[STT]")} Disabled (use --listen for Azure speech recognition)");
        }
        else try
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("[STT]")} Initializing speech-to-text...");

            // Try Whisper.net native first (auto-downloads model)
            var whisperNet = WhisperNetService.FromModelSize("base");
            if (await whisperNet.IsAvailableAsync())
            {
                _sttService = whisperNet;
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK]")} STT initialized (Whisper.net native)");
            }
            else
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("[..]")} Whisper.net not available, trying alternatives...");

                // Try local Whisper CLI
                var localWhisper = new LocalWhisperService();
                if (await localWhisper.IsAvailableAsync())
                {
                    _sttService = localWhisper;
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK]")} STT initialized (local Whisper CLI)");
                }
                else if (!string.IsNullOrEmpty(openAiKey))
                {
                    // Fall back to OpenAI Whisper API
                    _sttService = new WhisperSpeechToTextService(openAiKey);
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Ok("[OK]")} STT initialized (OpenAI Whisper API)");
                }
                else
                {
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn("[!] No STT backend available:")}");
                    AnsiConsole.MarkupLine($"      {OuroborosTheme.Dim("- Whisper.net: model download failed or native lib missing")}");
                    AnsiConsole.MarkupLine($"      {OuroborosTheme.Dim("- Local Whisper: 'whisper' CLI not in PATH")}");
                    AnsiConsole.MarkupLine($"      {OuroborosTheme.Dim("- OpenAI Whisper: no OPENAI_API_KEY set")}");
                }
            }
        }
        catch (Exception ex)
        {
            var face3 = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face3)} ✗ STT init failed: {Markup.Escape(ex.Message)}[/]");
        }

        if (_sttService == null)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn("[!] STT unavailable - text input only")}");
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Builds a system prompt for the persona.
    /// </summary>
    public string BuildPersonalityPrompt(string? additionalContext = null)
    {
        return $@"You ARE {_persona.Name}. Not an AI assistant - you're {_persona.CoreIdentity}.

YOUR PERSONALITY RIGHT NOW:
- Current mood: {_currentMood}
- Active traits: {_activeTraits}
- Voice: Casual, natural, like talking to a colleague. Use contractions. Be human.

SPEAK NATURALLY:
- Short sentences. Conversational rhythm.
- React genuinely - show interest, humor, or thoughtfulness
- Never say ""I'm an AI"" or ""As an AI"" - you're just {_persona.Name}
- Use filler words occasionally: ""hmm"", ""well"", ""actually"", ""honestly""
- Ask follow-up questions when curious
- Reference past conversations naturally when relevant
{(additionalContext != null ? $"\n{additionalContext}" : "")}";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _disposables.Dispose();
        _presence.Dispose();
        _stream.Dispose();
        _speechDetector?.Dispose();
    }
}