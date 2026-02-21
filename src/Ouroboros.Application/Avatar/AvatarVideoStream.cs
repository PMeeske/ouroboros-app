// <copyright file="AvatarVideoStream.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Application.Avatar;

/// <summary>
/// Runs the avatar video generation loop.
/// <list type="number">
///   <item>On each tick: reads the current <see cref="AvatarStateSnapshot"/>.</item>
///   <item>When a vision model is present: calls it to generate an expression description (text only; CSS renders).</item>
///   <item>Otherwise: applies <see cref="AlgorithmicExpressionGenerator"/> (GDI+ pixel transforms) and broadcasts the frame.</item>
///   <item>Publishes visual perception to <see cref="VirtualSelf"/> (closes the perception loop).</item>
/// </list>
/// </summary>
/// <param name="avatarService">The avatar service for state + broadcasting.</param>
/// <param name="visionModel">Optional vision model. When supplied, the vision model generates an expression
/// description on each state change (CSS renders visually). When absent, <see cref="AlgorithmicExpressionGenerator"/>
/// applies GDI+ pixel transforms and broadcasts JPEG frames directly.</param>
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

    /// <summary>
    /// Frame generation interval in milliseconds.
    /// Algorithmic generation is fast; default of 200 ms gives ~5 FPS.
    /// The vision-model path only fires on state changes so the interval there acts as a poll cadence.
    /// </summary>
    public int FrameIntervalMs { get; set; } = 200;

    /// <summary>
    /// Gets whether the video stream loop is currently running.
    /// </summary>
    public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;

    /// <summary>
    /// Starts the generation loop in the background.
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
        logger?.LogInformation("Avatar video stream started (interval: {Interval}ms)", FrameIntervalMs);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the generation loop.
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

        logger?.LogInformation("Avatar video stream stopped");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        AvatarVisualState? lastAnalyzedState = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var state = avatarService.CurrentState;
                var seedPath = AvatarVideoGenerator.GetSeedAssetPath(state.VisualState, _assetDirectory);

                if (!File.Exists(seedPath))
                {
                    logger?.LogWarning("Seed asset not found: {Path}", seedPath);
                    await Task.Delay(FrameIntervalMs, ct);
                    continue;
                }

                byte[] seedBytes = await File.ReadAllBytesAsync(seedPath, ct);

                if (visionModel != null)
                {
                    // ── Pure vision model path ──────────────────────────────────────
                    // Only re-analyse when the visual state actually changes — the seed
                    // image is static per state so calling on every tick is wasteful.
                    if (state.VisualState != lastAnalyzedState)
                    {
                        lastAnalyzedState = state.VisualState;
                        string expressionTarget = AvatarVideoGenerator.BuildPrompt(state);

                        // Vision model generates the expression description
                        var generated = await visionModel.AnswerQuestionAsync(
                            seedBytes, "png",
                            $"Describe this face's expression in 8 words or less, focusing on: {expressionTarget}",
                            ct);

                        if (generated.IsSuccess)
                        {
                            string description = generated.Value.Trim();

                            // Push generated description as avatar status text
                            avatarService.NotifyMoodChange(
                                mood: state.Mood ?? "neutral",
                                energy: 0.5,
                                positivity: 0.5,
                                statusText: description);

                            // Publish visual perception to VirtualSelf
                            virtualSelf?.PublishVisualPerception(
                                description: description,
                                objects: Array.Empty<DetectedObject>(),
                                faces: Array.Empty<DetectedFace>(),
                                sceneType: "avatar",
                                emotion: state.Mood,
                                rawFrame: seedBytes);

                            logger?.LogDebug("Vision model generated: {Description}", description);
                        }
                    }
                }
                else
                {
                    // ── Algorithmic expression path (no vision model) ───────────────
                    // Pure GDI+ pixel transforms: brightness/warmth per state + brow offset.
                    // No external service required — deterministic and instant.
                    byte[] frameBytes = AlgorithmicExpressionGenerator.ApplyExpression(seedBytes, state);
                    await avatarService.BroadcastVideoFrameAsync(frameBytes);
                    virtualSelf?.PublishVisualPerception(
                        description: AvatarVideoGenerator.BuildPrompt(state),
                        objects: Array.Empty<DetectedObject>(),
                        faces: Array.Empty<DetectedFace>(),
                        sceneType: "avatar",
                        emotion: state.Mood,
                        rawFrame: frameBytes);
                }
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error in avatar video generation loop");
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
