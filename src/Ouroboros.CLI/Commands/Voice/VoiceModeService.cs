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
public sealed class VoiceModeService : IDisposable
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
    /// Speaks text using TTS with console output.
    /// Uses Rx-based SpeechQueue for proper serialization with VoiceSideChannel.
    /// </summary>
    public async Task SayAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // If voice mode is not initialized, just print text (no TTS)
        if (!_isInitialized)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("[>]")} {OuroborosTheme.Accent(_persona.Name + ":")} {Markup.Escape(text)}");
            return;
        }

        // Sanitize for TTS
        string sanitized = SanitizeForTts(text);
        if (string.IsNullOrWhiteSpace(sanitized)) return;

        // Initialize SpeechQueue with our TTS
        Ouroboros.Domain.Autonomous.SpeechQueue.Instance.SetSynthesizer(async (t, p, ct) =>
        {
            await SpeakInternalAsync(t, isWhisper: false);
        });

        // Use Rx queue for proper serialization
        await Ouroboros.Domain.Autonomous.SpeechQueue.Instance.EnqueueAndWaitAsync(sanitized, _persona.Name);
    }

    /// <summary>
    /// Whispers text using TTS with a softer, more intimate voice style.
    /// Used for inner thoughts and reflections.
    /// </summary>
    public async Task WhisperAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // If voice mode is not initialized, just print text (no TTS)
        if (!_isInitialized)
        {
            AnsiConsole.MarkupLine($"  [rgb(128,0,180)]{Markup.Escape("[💭] " + text)}[/]");
            return;
        }

        // Sanitize for TTS
        string sanitized = SanitizeForTts(text);
        if (string.IsNullOrWhiteSpace(sanitized)) return;

        // Initialize SpeechQueue with whisper TTS
        Ouroboros.Domain.Autonomous.SpeechQueue.Instance.SetSynthesizer(async (t, p, ct) =>
        {
            await SpeakInternalAsync(t, isWhisper: true);
        });

        // Use Rx queue for proper serialization
        await Ouroboros.Domain.Autonomous.SpeechQueue.Instance.EnqueueAndWaitAsync(sanitized, _persona.Name);
    }

    /// <summary>
    /// Internal speech method - does the actual TTS work.
    /// Uses Rx streaming for presence state and voice output events.
    /// </summary>
    private async Task SpeakInternalAsync(string sanitized, bool isWhisper = false)
    {
        _isSpeaking = true;
        _speechDetector?.NotifySelfSpeechStarted();

        // Set presence state to Speaking (or Thinking for whisper/inner thoughts)
        _stream.SetPresenceState(
            isWhisper ? AgentPresenceState.Speaking : AgentPresenceState.Speaking,
            isWhisper ? "Thinking aloud" : "Speaking");

        try
        {
            if (!_config.VoiceOnly)
            {
                if (isWhisper)
                {
                    AnsiConsole.Markup($"  [rgb(128,0,180)]{Markup.Escape("[💭]")}[/] ");
                }
                else
                {
                    AnsiConsole.Markup($"  {OuroborosTheme.Accent("[>]")} {OuroborosTheme.Accent(_persona.Name + ":")} ");
                }
            }

            // Publish the response event to Rx stream
            _stream.PublishResponse(sanitized, isComplete: true, isSentenceEnd: true);

            // Priority: Azure Neural TTS > Local SAPI > Cloud TTS
            // With automatic fallback on rate limiting (429) or other errors
            bool ttsSucceeded = false;

            if (_azureTts != null)
            {
                if (!_config.VoiceOnly) AnsiConsole.MarkupLine(Markup.Escape(sanitized));
                try
                {
                    await _azureTts.SpeakAsync(sanitized, isWhisper);
                    ttsSucceeded = true;
                }
                catch (Exception ex)
                {
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Azure TTS failed: {Markup.Escape(ex.Message)}, trying fallback...[/]");
                }
            }

            // Fallback to Edge TTS (neural quality, free, no rate limits)
            // Skip if circuit is open (Microsoft blocking the unofficial API)
            if (!ttsSucceeded && _edgeTts != null && !EdgeTtsService.IsCircuitOpen)
            {
                try
                {
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("[>]")} Trying Edge TTS (neural, free)...");
                    TextToSpeechOptions? options = isWhisper ? new TextToSpeechOptions(IsWhisper: true) : null;
                    Result<SpeechResult, string> edgeResult = await _edgeTts.SynthesizeAsync(sanitized, options);
                    if (edgeResult.IsSuccess)
                    {
                        await AudioPlayer.PlayAsync(edgeResult.Value);
                        ttsSucceeded = true;
                    }
                    else
                    {
                        var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Edge TTS failed: {Markup.Escape(edgeResult.Error)}[/]");
                    }
                }
                catch (Exception ex)
                {
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Edge TTS failed: {Markup.Escape(ex.Message)}[/]");
                }
            }

            // Fallback to local TTS if Edge also failed (offline fallback)
            if (!ttsSucceeded && _localTts != null)
            {
                try
                {
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("[>]")} Trying Windows SAPI (offline fallback)...");
                    await SpeakWithLocalTtsAsync(sanitized, isWhisper);
                    ttsSucceeded = true;
                }
                catch (Exception ex)
                {
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Local TTS failed: {Markup.Escape(ex.Message)}[/]");
                }
            }

            // Fallback to cloud TTS (OpenAI)
            if (!ttsSucceeded && _ttsService != null && _ttsService != _azureTts && _ttsService != _localTts)
            {
                try
                {
                    if (_azureTts == null && _localTts == null && !_config.VoiceOnly) AnsiConsole.MarkupLine(Markup.Escape(sanitized));
                    await SpeakWithCloudTtsAsync(sanitized);
                    ttsSucceeded = true;
                }
                catch (Exception ex)
                {
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Cloud TTS failed: {Markup.Escape(ex.Message)}[/]");
                }
            }

            if (!ttsSucceeded)
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn("[!] All TTS services failed - voice output skipped")}");
            }
        }
        finally
        {
            await Task.Delay(300);
            _isSpeaking = false;
            _speechDetector?.NotifySelfSpeechEnded(cooldownMs: 400);

            // Return to Idle state
            _stream.SetPresenceState(AgentPresenceState.Idle, "Finished speaking");
        }
    }

    /// <summary>
    /// Listens for voice input using STT.
    /// Uses Rx streaming for presence state and voice input events.
    /// </summary>
    public async Task<string?> ListenAsync(CancellationToken ct = default)
    {
        if (_sttService == null) return null;
        if (_isSpeaking) return null;

        // Set presence state to Listening
        _stream.SetPresenceState(AgentPresenceState.Listening, "Listening for voice input");

        try
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"speech_{Guid.NewGuid()}.wav");

            var recordResult = await MicrophoneRecorder.RecordAsync(
                durationSeconds: 5,
                outputPath: tempFile,
                ct: ct);

            string? audioFile = null;
            recordResult.Match(f => audioFile = f, _ => { });

            if (audioFile != null && File.Exists(audioFile))
            {
                // Set presence state to Processing while transcribing
                _stream.SetPresenceState(AgentPresenceState.Processing, "Transcribing speech");

                try
                {
                    var transcribeResult = await _sttService.TranscribeFileAsync(audioFile, null, ct);
                    string? transcript = null;
                    transcribeResult.Match(t => transcript = t.Text, _ => { });

                    if (!string.IsNullOrWhiteSpace(transcript))
                    {
                        var trimmed = transcript.Trim();

                        // Publish voice input event to Rx stream
                        _stream.PublishVoiceInput(trimmed, confidence: 1.0);
                        _stream.SetPresenceState(AgentPresenceState.Idle, "Voice input received");

                        return trimmed;
                    }
                }
                finally
                {
                    try { File.Delete(audioFile); } catch { }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _stream.SetPresenceState(AgentPresenceState.Idle, "Listening cancelled");
        }
        catch (Exception ex)
        {
            var face4 = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face4)} ✗ Listen error: {Markup.Escape(ex.Message)}[/]");
            _stream.PublishError(ex.Message, ex, ErrorCategory.SpeechRecognition);
        }

        _stream.SetPresenceState(AgentPresenceState.Idle, "No voice input detected");
        return null;
    }

    /// <summary>
    /// Gets input from either voice or keyboard simultaneously using Rx.
    /// Uses non-blocking keyboard polling and parallel voice recording.
    /// </summary>
    public async Task<string?> GetInputAsync(string prompt = "You: ", CancellationToken ct = default)
    {
        AnsiConsole.Markup(Markup.Escape(prompt));

        // If no STT or no mic, keyboard only
        if (_sttService == null || !MicrophoneRecorder.IsRecordingAvailable())
        {
            return await Task.Run(() => Console.ReadLine(), ct);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var inputBuffer = new System.Text.StringBuilder();
        string? finalResult = null;

        // Keyboard observable - polls Console.KeyAvailable every 50ms
        var keyboardStream = Observable
            .Interval(TimeSpan.FromMilliseconds(50))
            .TakeWhile(_ => !cts.Token.IsCancellationRequested)
            .Select(_ =>
            {
                try
                {
                    while (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true); // Intercept to prevent double echo
                        if (key.Key == ConsoleKey.Enter)
                        {
                            Console.WriteLine(); // Move to next line
                            var result = inputBuffer.ToString();
                            inputBuffer.Clear();
                            return result;
                        }
                        else if (key.Key == ConsoleKey.Backspace)
                        {
                            if (inputBuffer.Length > 0)
                            {
                                inputBuffer.Length--;
                                Console.Write("\b \b"); // Erase character visually
                            }
                        }
                        else if (!char.IsControl(key.KeyChar))
                        {
                            inputBuffer.Append(key.KeyChar);
                            Console.Write(key.KeyChar); // Echo character manually
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // Console redirected - fall through
                }
                return (string?)null;
            })
            .Where(s => s != null)
            .Take(1);

        // Voice observable - starts recording after 500ms delay, gives keyboard priority
        var voiceStream = Observable
            .Timer(TimeSpan.FromMilliseconds(500))
            .SelectMany(_ => Observable.FromAsync(async token =>
            {
                if (cts.Token.IsCancellationRequested) return null;
                try
                {
                    return await ListenAsync(cts.Token);
                }
                catch
                {
                    return null;
                }
            }))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Do(s => AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]{Markup.Escape("🎤 [" + s + "]")}[/]"))
            .Take(1);

        // Race both streams - first valid input wins
        var resultObservable = keyboardStream
            .Merge(voiceStream)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Take(1)
            .Timeout(TimeSpan.FromMinutes(5))
            .Finally(() =>
            {
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // CTS already disposed - safe to ignore
                }
            });

        try
        {
            finalResult = await resultObservable.FirstOrDefaultAsync();
        }
        catch (TimeoutException)
        {
            finalResult = null;
        }
        catch (InvalidOperationException)
        {
            // Sequence contains no elements - both streams completed without result
            finalResult = null;
        }

        return finalResult;
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

    /// <summary>
    /// Prints voice mode header.
    /// </summary>
    public void PrintHeader(string commandName)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule($"VOICE MODE - {commandName.ToUpperInvariant()} ({_persona.Name})"));
        var table = OuroborosTheme.ThemedTable("Property", "Value");
        table.AddRow(OuroborosTheme.Accent("Personality:"), Markup.Escape(_activeTraits));
        table.AddRow(OuroborosTheme.Accent("Mood:"), Markup.Escape(_currentMood));
        table.AddRow("", OuroborosTheme.Dim("Say 'help' for commands, 'goodbye' or 'exit' to quit"));
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static string SanitizeForTts(string text)
    {
        // Remove code blocks and inline code
        var sanitized = System.Text.RegularExpressions.Regex.Replace(text, @"```[\s\S]*?```", " ");
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"`[^`]+`", " ");

        // Remove emojis - keep only ASCII printable and extended Latin
        var sb = new System.Text.StringBuilder();
        foreach (var c in sanitized)
        {
            if ((c >= 32 && c <= 126) || (c >= 192 && c <= 255))
            {
                sb.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                sb.Append(' ');
            }
        }

        // Normalize whitespace
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private async Task SpeakWithLocalTtsAsync(string text, bool isWhisper = false)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        try
        {
            // For local SAPI, adjust rate and volume for whispering effect
            if (isWhisper && _localTts != null)
            {
                // SAPI doesn't have true whisper, but we can simulate with lower volume/rate
                // This would require modifying LocalWindowsTtsService
            }

            var wordStream = Observable.Create<string>(async (observer, ct) =>
            {
                var chunks = SplitIntoSemanticChunks(text, words);
                foreach (var chunk in chunks)
                {
                    if (ct.IsCancellationRequested) break;
                    if (string.IsNullOrWhiteSpace(chunk)) continue;

                    var chunkWords = chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var speakTask = _localTts!.SpeakAsync(chunk);

                    foreach (var word in chunkWords)
                    {
                        observer.OnNext(word);
                    }

                    await speakTask;
                }
                observer.OnCompleted();
            });

            if (!_config.VoiceOnly)
            {
                if (isWhisper)
                {
                    await wordStream.ForEachAsync(word => AnsiConsole.Markup($"[grey]{Markup.Escape(word + " ")}[/]"));
                }
                else
                {
                    await wordStream.ForEachAsync(word => AnsiConsole.Markup(Markup.Escape(word + " ")));
                }
                AnsiConsole.WriteLine();
            }
            else
            {
                await wordStream.LastOrDefaultAsync();
            }
        }
        catch (Exception ex)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"\n  [red]{Markup.Escape(face)} ✗ TTS error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private async Task SpeakWithCloudTtsAsync(string text)
    {
        try
        {
            var voice = _persona.Voice switch
            {
                "nova" => TtsVoice.Nova,
                "echo" => TtsVoice.Echo,
                "onyx" => TtsVoice.Onyx,
                "fable" => TtsVoice.Fable,
                "shimmer" => TtsVoice.Shimmer,
                _ => TtsVoice.Nova
            };

            var options = new TextToSpeechOptions(Voice: voice, Speed: 1.0f, Format: "mp3");
            var result = await _ttsService!.SynthesizeAsync(text, options);

            await result.Match(
                async speech =>
                {
                    if (!_config.VoiceOnly) AnsiConsole.MarkupLine(Markup.Escape(text));
                    var playResult = await AudioPlayer.PlayAsync(speech);
                    playResult.Match(_ => { }, err =>
                    {
                        var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Playback: {Markup.Escape(err)}[/]");
                    });
                },
                err =>
                {
                    if (!_config.VoiceOnly) AnsiConsole.MarkupLine(Markup.Escape(text));
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ TTS: {Markup.Escape(err)}[/]");
                    return Task.CompletedTask;
                });
        }
        catch (Exception ex)
        {
            if (!_config.VoiceOnly) AnsiConsole.MarkupLine(Markup.Escape(text));
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ TTS error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private static IEnumerable<string> SplitIntoSemanticChunks(string text, string[] words)
    {
        var semanticPattern = new System.Text.RegularExpressions.Regex(
            @"(?<=[.!?])\s+|(?<=[;:,—–])\s+|(?<=\band\b|\bor\b|\bbut\b|\bso\b|\bthen\b)\s+",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var chunks = semanticPattern.Split(text)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        if (chunks.Count <= 1 && words.Length > 8)
        {
            chunks.Clear();
            for (int i = 0; i < words.Length; i += 8)
            {
                chunks.Add(string.Join(" ", words.Skip(i).Take(8)));
            }
        }

        return chunks;
    }

    private static bool CheckWhisperAvailable()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "whisper",
                Arguments = "--help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(startInfo);
            return process != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets up the Rx display pipeline for colored console output.
    /// </summary>
    private void SetupDisplayPipeline()
    {
        // Display text output events with styling
        _disposables.Add(
            _stream.TextOutputs
                .Subscribe(e =>
                {
                    var escaped = Markup.Escape(e.Text);
                    var styled = e.Style switch
                    {
                        OutputStyle.Thinking => $"[grey]{escaped}[/]",
                        OutputStyle.Emphasis => $"[rgb(148,103,189)]{escaped}[/]",
                        OutputStyle.Whisper => $"[rgb(128,0,180)]{escaped}[/]",
                        OutputStyle.System => $"[yellow]{escaped}[/]",
                        OutputStyle.Error => $"[red]{escaped}[/]",
                        OutputStyle.UserInput => $"[green]{escaped}[/]",
                        _ => escaped,
                    };

                    if (e.Append)
                    {
                        AnsiConsole.Markup(styled);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine(styled);
                    }
                }));

        // Display errors
        _disposables.Add(
            _stream.Errors
                .Subscribe(e =>
                {
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"\n  [red]{Markup.Escape(face)} ✗ {Markup.Escape(e.Category.ToString())}: {Markup.Escape(e.Message)}[/]");
                }));
    }

    /// <summary>
    /// Sets up visual presence state indicators.
    /// </summary>
    private void SetupPresenceIndicators()
    {
        // Show visual state indicators [ ]/[*]/[...]/[>]
        _disposables.Add(
            _presence.State
                .DistinctUntilChanged()
                .Subscribe(state =>
                {
                    if (!_enableVisualIndicators || _config.VoiceOnly) return;

                    var indicator = state switch
                    {
                        AgentPresenceState.Idle => "[ ]",
                        AgentPresenceState.Listening => "[*]",
                        AgentPresenceState.Processing => "[...]",
                        AgentPresenceState.Speaking => "[>]",
                        AgentPresenceState.Interrupted => "[!]",
                        AgentPresenceState.Paused => "[-]",
                        _ => "[ ]",
                    };

                    AnsiConsole.Markup($"\r{Markup.Escape(indicator)} ");
                }));

        // Subscribe to barge-in events
        _presence.BargeInDetected += (_, e) =>
        {
            var snippet = e.UserInput?[..Math.Min(30, e.UserInput?.Length ?? 0)] ?? "";
            AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Warn("[Interrupted] " + snippet + "...")}");
        };
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