// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Collections.Concurrent;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

/// <summary>
/// Manages all voice-related capabilities: TTS, STT, voice channels, and listening services.
/// </summary>
public interface IVoiceSubsystem : IAgentSubsystem
{
    VoiceModeService Service { get; }
    VoiceModeServiceV2? V2 { get; }
    VoiceSideChannel? SideChannel { get; }
    EnhancedListeningService? Listener { get; }
    bool IsListening { get; }
}

/// <summary>
/// Voice subsystem implementation owning voice services, speech processes, and listening.
/// </summary>
public sealed class VoiceSubsystem : IVoiceSubsystem
{
    private static readonly ConcurrentBag<System.Diagnostics.Process> _activeSpeechProcesses = new();

    public string Name => "Voice";
    public bool IsInitialized { get; private set; }

    // Owned components
    public VoiceModeService Service { get; }
    public VoiceModeServiceV2? V2 { get; set; }
    public VoiceSideChannel? SideChannel { get; set; }
    public EnhancedListeningService? Listener { get; set; }
    public bool IsListening { get; set; }

    // Listening state
    public CancellationTokenSource? ListeningCts { get; set; }
    public Task? ListeningTask { get; set; }

    public VoiceSubsystem(VoiceModeService voiceService)
    {
        Service = voiceService;
    }

    /// <summary>Delegate: agent provides its SpeakWithSapiAsync for voice side channel.</summary>
    public Func<string, Ouroboros.Domain.Autonomous.PersonaVoice, CancellationToken, Task>? SpeakWithSapiFunc { get; set; }

    // Cross-subsystem context (set during InitializeAsync)
    internal SubsystemInitContext Ctx { get; private set; } = null!;

    public void MarkInitialized() => IsInitialized = true;

    /// <inheritdoc/>
    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        Ctx = ctx;

        // Voice side channel
        if (ctx.Config.VoiceChannel)
        {
            try
            {
                SideChannel = new VoiceSideChannel(maxQueueSize: 15);
                SideChannel.SetDefaultPersona(ctx.Config.Persona);

                if (SpeakWithSapiFunc != null)
                {
                    SideChannel.SetSynthesizer(async (text, voice, ct) =>
                        await SpeakWithSapiFunc(text, voice, ct));
                }

                SideChannel.MessageSpoken += (_, msg) =>
                {
                    if (ctx.Config.Debug)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"  \ud83d\udd0a [{msg.PersonaName}] spoke: {msg.Text[..Math.Min(50, msg.Text.Length)]}...");
                        Console.ResetColor();
                    }
                };
                ctx.Output.RecordInit("Voice Side Channel", true, $"{ctx.Config.Persona} (parallel playback)");
            }
            catch (Exception ex)
            {
                ctx.Output.RecordInit("Voice Side Channel", false, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        // Voice V2 (Unified Rx Streaming) — created here, wired by agent mediator
        if (ctx.Config.VoiceV2)
        {
            try
            {
                Console.WriteLine("  [>] Initializing Voice V2 (Unified Rx Streaming)...");
                var v2Config = new VoiceModeConfigV2(
                    Persona: ctx.Config.Persona,
                    VoiceOnly: ctx.Config.VoiceOnly,
                    EnableTts: true, EnableStt: true,
                    EnableVisualIndicators: true,
                    Culture: ctx.Config.Culture);

                V2 = new VoiceModeServiceV2(v2Config);
                await V2.InitializeAsync();
                ctx.Output.RecordInit("Voice V2", true, "Unified Rx streaming");
            }
            catch (Exception ex)
            {
                ctx.Output.RecordInit("Voice V2", false, ex.Message);
            }
        }

        MarkInitialized();
    }

    /// <summary>
    /// Registers a speech process for cleanup on dispose.
    /// </summary>
    public static void TrackSpeechProcess(System.Diagnostics.Process process)
        => _activeSpeechProcesses.Add(process);

    /// <summary>
    /// Kills all tracked speech processes.
    /// </summary>
    public static void KillAllSpeechProcesses()
    {
        while (_activeSpeechProcesses.TryTake(out var process))
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                process.Dispose();
            }
            catch
            {
                // Swallow - best effort cleanup
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Cancel listening
        ListeningCts?.Cancel();
        if (ListeningTask != null)
        {
            try { await ListeningTask; } catch { /* expected */ }
        }
        ListeningCts?.Dispose();

        // Dispose enhanced listener
        if (Listener != null)
            await Listener.DisposeAsync();

        // Dispose voice side channel (drains queue)
        if (SideChannel != null)
            await SideChannel.DisposeAsync();

        // Dispose VoiceV2 (unified Rx streaming)
        if (V2 != null)
            await V2.DisposeAsync();

        // Dispose main voice service
        Service.Dispose();

        // Kill remaining speech processes
        KillAllSpeechProcesses();

        IsInitialized = false;
    }
}
