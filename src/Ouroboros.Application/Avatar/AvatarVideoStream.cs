// <copyright file="AvatarVideoStream.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Application.Avatar;

/// <summary>
/// Maintains the VirtualSelf visual-perception loop for the avatar.
/// On each state/mood change it calls <see cref="VirtualSelf.PublishVisualPerception"/>
/// so that Iaret's self-model stays in sync with her displayed state.
///
/// Thought generation (StatusText) is intentionally NOT handled here; it is driven
/// by the agent pipeline's <c>AutonomousMind.OnThought</c> event so that bubbles
/// contain real research/reflection content rather than a lookup table.
/// </summary>
/// <param name="avatarService">The avatar service (read-only; not written to from here).</param>
/// <param name="visionModel">Optional vision model — reserved for future use.</param>
/// <param name="virtualSelf">Optional VirtualSelf for perception loop closure.</param>
/// <param name="assetDirectory">Directory containing avatar image assets.</param>
/// <param name="logger">Optional logger.</param>
public sealed class AvatarVideoStream(
    InteractiveAvatarService avatarService,
    IVisionModel? visionModel = null,
    VirtualSelf? virtualSelf = null,
    string? assetDirectory = null,
    ILogger<AvatarVideoStream>? logger = null) : IAsyncDisposable
{
    private readonly string _assetDirectory = assetDirectory ?? GetDefaultAssetDirectory();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    /// <summary>Poll cadence in milliseconds.</summary>
    public int FrameIntervalMs { get; set; } = 1000;

    /// <summary>Gets whether the loop is currently running.</summary>
    public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;

    /// <summary>Starts the perception loop in the background.</summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning) return Task.CompletedTask;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
        logger?.LogInformation("Avatar perception loop started");
        return Task.CompletedTask;
    }

    /// <summary>Stops the loop.</summary>
    public async Task StopAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            if (_loopTask != null)
            {
                try { await _loopTask; }
                catch (OperationCanceledException) { }
            }
        }
        logger?.LogInformation("Avatar perception loop stopped");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        AvatarVisualState? lastState = null;
        string? lastMood = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var state = avatarService.CurrentState;
                bool changed = state.VisualState != lastState || state.Mood != lastMood;

                if (changed && virtualSelf != null)
                {
                    lastState = state.VisualState;
                    lastMood  = state.Mood;

                    // Publish visual state to VirtualSelf so Iaret's self-model stays current.
                    // Thought content is driven externally (AutonomousMind.OnThought) — not here.
                    virtualSelf.PublishVisualPerception(
                        description: AvatarVideoGenerator.BuildPrompt(state),
                        objects:     Array.Empty<DetectedObject>(),
                        faces:       Array.Empty<DetectedFace>(),
                        sceneType:   "avatar",
                        emotion:     state.Mood,
                        rawFrame:    Array.Empty<byte>());
                }
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error in avatar perception loop");
            }

            await Task.Delay(FrameIntervalMs, ct);
        }
    }

    private static string GetDefaultAssetDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Assets", "Avatar"),
            Path.Combine(baseDir, "..", "..", "..", "Assets", "Avatar"),
            Path.Combine(baseDir, "..", "..", "..", "..", "Ouroboros.CLI", "Assets", "Avatar"),
        };

        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (Directory.Exists(full)) return full;
        }

        return Path.Combine(baseDir, "Assets", "Avatar");
    }
}
