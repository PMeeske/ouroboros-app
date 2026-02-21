// <copyright file="AvatarVideoStream.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Application.Avatar;

/// <summary>
/// Runs the avatar inner-thought loop.
/// <list type="number">
///   <item>Polls the current <see cref="AvatarStateSnapshot"/> on each tick.</item>
///   <item>On state or mood change, generates Iaret's first-person inner thought.</item>
///   <item>When a vision model is present: asks it directly ("what are you thinking?").</item>
///   <item>Otherwise: picks from <see cref="AlgorithmicExpressionGenerator.GetInnerThought"/>.</item>
///   <item>Pushes the thought as <c>StatusText</c> via <see cref="InteractiveAvatarService.NotifyMoodChange"/>.</item>
///   <item>Publishes the thought as visual perception to <see cref="VirtualSelf"/>.</item>
/// </list>
/// Visual expression changes (idle / listening / thinking / speaking / encouraging) are
/// handled automatically by CSS state classes in the HTML viewer — no frame generation needed.
/// </summary>
/// <param name="avatarService">The avatar service for state + broadcasting.</param>
/// <param name="visionModel">Optional vision model. When supplied, the vision model generates Iaret's
/// first-person inner thought on each state change. When absent, a lookup table is used.</param>
/// <param name="virtualSelf">Optional VirtualSelf for perception loop closure.</param>
/// <param name="assetDirectory">Directory containing avatar image assets (used to load the seed
/// image that is passed to the vision model for context).</param>
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

    /// <summary>
    /// State-change poll interval in milliseconds.
    /// The loop only acts when state or mood changes, so a 1-second cadence is sufficient.
    /// </summary>
    public int FrameIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Gets whether the loop is currently running.
    /// </summary>
    public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;

    /// <summary>
    /// Starts the inner-thought loop in the background.
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
        logger?.LogInformation("Avatar thought loop started (poll: {Interval}ms)", FrameIntervalMs);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the loop.
    /// </summary>
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

        logger?.LogInformation("Avatar thought loop stopped");
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

                // Only act when visual state or mood changes.
                bool changed = state.VisualState != lastState || state.Mood != lastMood;
                if (changed)
                {
                    lastState = state.VisualState;
                    lastMood  = state.Mood;

                    string thought;

                    if (visionModel != null)
                    {
                        // ── Vision model path ───────────────────────────────────────
                        // Load the seed image so the model has visual context of Iaret's
                        // current face, then ask for her first-person inner thought.
                        var seedPath = AvatarVideoGenerator.GetSeedAssetPath(state.VisualState, _assetDirectory);
                        byte[]? seedBytes = File.Exists(seedPath)
                            ? await File.ReadAllBytesAsync(seedPath, ct)
                            : null;

                        string prompt =
                            $"You are Iaret. You are {state.VisualState.ToString().ToLower()}, " +
                            $"feeling {state.Mood ?? "neutral"}. " +
                            "What is your inner thought right now? " +
                            "First person only, 10 words or less, no quotes.";

                        Result<string, string> generated = seedBytes != null
                            ? await visionModel.AnswerQuestionAsync(seedBytes, "png", prompt, ct)
                            : Result<string, string>.Failure("no seed image");

                        if (generated.IsSuccess)
                        {
                            thought = generated.Value.Trim();
                            logger?.LogDebug("Vision model thought: {Thought}", thought);
                        }
                        else
                        {
                            // Fallback to lookup if vision model fails
                            thought = AlgorithmicExpressionGenerator.GetInnerThought(state);
                        }
                    }
                    else
                    {
                        // ── Lookup path ─────────────────────────────────────────────
                        thought = AlgorithmicExpressionGenerator.GetInnerThought(state);
                    }

                    // Push thought as avatar status text
                    avatarService.NotifyMoodChange(
                        mood:       state.Mood ?? "neutral",
                        energy:     state.Energy,
                        positivity: state.Positivity,
                        statusText: thought);

                    // Publish to VirtualSelf (closes perception loop)
                    virtualSelf?.PublishVisualPerception(
                        description: thought,
                        objects:     Array.Empty<DetectedObject>(),
                        faces:       Array.Empty<DetectedFace>(),
                        sceneType:   "avatar",
                        emotion:     state.Mood,
                        rawFrame:    null);
                }
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error in avatar thought loop");
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
