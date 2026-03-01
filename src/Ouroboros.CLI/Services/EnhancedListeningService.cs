// <copyright file="EnhancedListeningService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Disposables;
using System.Reactive.Linq;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Domain.Voice;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Speech;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Enhanced listening service that replaces the simple ListenLoopAsync polling approach.
/// Provides continuous streaming STT, wake word detection, barge-in support,
/// and local Whisper.net fallback — built on existing infrastructure.
/// </summary>
public sealed partial class EnhancedListeningService : IAsyncDisposable
{
    private readonly OuroborosConfig _config;
    private readonly IConsoleOutput _output;
    private readonly Func<string, Task<string>> _processInput;
    private readonly Func<string, CancellationToken, Task> _speak;

    // Reactive infrastructure
    private readonly InteractionStream _stream;
    private readonly AgentPresenceController _presence;
    private readonly AdaptiveSpeechDetector _vad;
    private readonly CompositeDisposable _disposables = new();

    // STT backends
    private AzureStreamingSttService? _azureStt;
    private WhisperNetService? _whisperStt;

    // Azure continuous recognition (direct mic mode)
    private SpeechRecognizer? _recognizer;

    // State
    private bool _isAwake;
    private CancellationTokenSource? _currentTtsCts;
    private CancellationTokenSource? _currentProcessingCts;
    private Task? _wakeWordTimeout;
    private bool _isProcessing;
    private bool _disposed;

    /// <summary>
    /// Creates a new EnhancedListeningService.
    /// </summary>
    /// <param name="config">Agent configuration.</param>
    /// <param name="output">Console output service.</param>
    /// <param name="processInput">Delegate to process user input (ChatAsync).</param>
    /// <param name="speak">Delegate to speak response (SpeakResponseWithAzureTtsAsync).</param>
    public EnhancedListeningService(
        OuroborosConfig config,
        IConsoleOutput output,
        Func<string, Task<string>> processInput,
        Func<string, CancellationToken, Task> speak)
    {
        _config = config;
        _output = output;
        _processInput = processInput;
        _speak = speak;

        _stream = new InteractionStream();
        _presence = new AgentPresenceController(_stream);
        _vad = new AdaptiveSpeechDetector(new AdaptiveSpeechDetector.SpeechDetectionConfig());

        // Wake word: null means always-on
        _isAwake = config.WakeWord == null;

        // Wire barge-in
        _presence.BargeInDetected += OnBargeIn;
    }

    /// <summary>
    /// Starts the enhanced listening pipeline.
    /// </summary>
    public async Task StartAsync(CancellationToken ct)
    {
        var backend = ResolveBackend();

        if (backend == "azure")
        {
            await StartAzureContinuousAsync(ct);
        }
        else
        {
            await StartWhisperFallbackAsync(ct);
        }
    }

    /// <summary>
    /// Resolves which STT backend to use.
    /// </summary>
    private string ResolveBackend()
    {
        if (_config.SttBackend.Equals("azure", StringComparison.OrdinalIgnoreCase))
            return "azure";

        if (_config.SttBackend.Equals("whisper", StringComparison.OrdinalIgnoreCase))
            return "whisper";

        // Auto: prefer Azure if key is available
        var azureKey = _config.AzureSpeechKey
                       ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");

        if (!string.IsNullOrEmpty(azureKey))
            return "azure";

        _output.WriteSystem("No Azure Speech key found, falling back to Whisper (local)");
        return "whisper";
    }

    // ════════════════════════════════════════════════════════════════
    // DISPOSAL
    // ════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_recognizer != null)
        {
            try
            {
                await _recognizer.StopContinuousRecognitionAsync();
            }
            catch
            {
                // Best effort
            }

            _recognizer.Dispose();
        }

        _currentTtsCts?.Cancel();
        _currentTtsCts?.Dispose();
        _currentProcessingCts?.Cancel();
        _currentProcessingCts?.Dispose();

        _presence.BargeInDetected -= OnBargeIn;
        _presence.Dispose();
        _stream.Dispose();
        _vad.Dispose();
        _disposables.Dispose();

        (_whisperStt as IDisposable)?.Dispose();
    }
}
