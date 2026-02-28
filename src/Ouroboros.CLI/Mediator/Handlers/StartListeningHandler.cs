using MediatR;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Subsystems;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="StartListeningRequest"/>.
/// Creates an <see cref="Ouroboros.CLI.Services.EnhancedListeningService"/> and launches
/// the listening task. Extracted from <c>OuroborosAgent.StartListeningAsync</c>.
/// </summary>
public sealed class StartListeningHandler : IRequestHandler<StartListeningRequest, Unit>
{
    private readonly OuroborosAgent _agent;

    public StartListeningHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<Unit> Handle(StartListeningRequest request, CancellationToken ct)
    {
        var voiceSub = _agent.VoiceSub;

        voiceSub.ListeningCts = new CancellationTokenSource();
        voiceSub.IsListening = true;

        _agent.ConsoleOutput.WriteSystem(
            _agent.LocalizationSub.GetLocalizedString("listening_start"));

        // Create the enhanced listening service.
        // processInput delegates to the MediatR ChatRequest pipeline.
        // speak delegates to the private SpeakResponseWithAzureTtsAsync helper below.
        voiceSub.Listener = new Ouroboros.CLI.Services.EnhancedListeningService(
            _agent.Config,
            _agent.ConsoleOutput,
            processInput: input => _agent.Mediator.Send(new ChatRequest(input)),
            speak: (text, speakCt) => SpeakResponseWithAzureTtsAsync(text,
                _agent.Config.AzureSpeechKey ?? System.Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY") ?? "",
                _agent.Config.AzureSpeechRegion,
                speakCt));

        voiceSub.ListeningTask = Task.Run(async () =>
        {
            try
            {
                await voiceSub.Listener.StartAsync(voiceSub.ListeningCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (InvalidOperationException ex)
            {
                _agent.ConsoleOutput.WriteError($"Listening error: {ex.Message}");
            }
            finally
            {
                voiceSub.IsListening = false;
            }
        });

        await Task.CompletedTask;
        return Unit.Value;
    }

    /// <summary>
    /// Speaks a response using Azure TTS with configured voice.
    /// Supports barge-in via the CancellationToken - cancelling stops synthesis immediately.
    /// Migrated from <c>OuroborosAgent.SpeakResponseWithAzureTtsAsync</c>.
    /// </summary>
    private async Task SpeakResponseWithAzureTtsAsync(string text, string key, string region, CancellationToken ct)
    {
        try
        {
            var config = Microsoft.CognitiveServices.Speech.SpeechConfig.FromSubscription(key, region);

            // Use configured voice or default multilingual voice
            var voiceName = _agent.Config.TtsVoice ?? "en-US-JennyMultilingualNeural";
            config.SpeechSynthesisVoiceName = voiceName;

            // Use default speaker
            using var speechSynthesizer = new Microsoft.CognitiveServices.Speech.SpeechSynthesizer(config);

            // Register cancellation to stop synthesis (barge-in support)
            ct.Register(() =>
            {
                try { _ = speechSynthesizer.StopSpeakingAsync(); }
                catch (InvalidOperationException) { /* Best effort barge-in stop */ }
            });

            // Detect the response language via LanguageSubsystem (Ollama LLM -> heuristic fallback).
            var voicePrimaryLocale = voiceName.Length >= 5 ? voiceName[..5] : "en-US";
            var responseLang = await LanguageSubsystem
                .DetectStaticAsync(text, ct).ConfigureAwait(false);
            var voiceLang = responseLang.Culture;
            var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{voicePrimaryLocale}'>
    <voice name='{voiceName}' xml:lang='{voiceLang}'>
        {System.Net.WebUtility.HtmlEncode(text)}
    </voice>
</speak>";

            var result = await speechSynthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason != Microsoft.CognitiveServices.Speech.ResultReason.SynthesizingAudioCompleted
                && _agent.Config.Debug)
            {
                _agent.ConsoleOutput.WriteDebug($"[Azure TTS] Synthesis issue: {result.Reason}");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during barge-in
        }
        catch (InvalidOperationException ex)
        {
            if (_agent.Config.Debug)
            {
                _agent.ConsoleOutput.WriteDebug($"[Azure TTS] Error: {ex.Message}");
            }
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            if (_agent.Config.Debug)
            {
                _agent.ConsoleOutput.WriteDebug($"[Azure TTS] Error: {ex.Message}");
            }
        }
    }
}
