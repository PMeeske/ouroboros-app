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
///   <item>Calls <see cref="AvatarVideoGenerator.GenerateFrameAsync"/> with the seed asset + emotional prompt.</item>
///   <item>Broadcasts binary frame via <see cref="InteractiveAvatarService"/>.</item>
///   <item>Publishes visual perception to <see cref="VirtualSelf"/> (closes the perception loop).</item>
/// </list>
/// </summary>
public sealed class AvatarVideoStream : IAsyncDisposable
{
    private readonly AvatarVideoGenerator _generator;
    private readonly InteractiveAvatarService _avatarService;
    private readonly IVisionModel? _visionModel;
    private readonly VirtualSelf? _virtualSelf;
    private readonly string _assetDirectory;
    private readonly ILogger<AvatarVideoStream>? _logger;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    /// <summary>
    /// Default frame generation interval in milliseconds.
    /// SD is computationally expensive, so ~1/3 FPS is a reasonable default.
    /// </summary>
    public int FrameIntervalMs { get; set; } = 3000;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvatarVideoStream"/> class.
    /// </summary>
    /// <param name="generator">The Ollama SD frame generator.</param>
    /// <param name="avatarService">The avatar service for state + broadcasting.</param>
    /// <param name="visionModel">Optional vision model (not required for generation, but available for future use).</param>
    /// <param name="virtualSelf">Optional VirtualSelf for perception loop closure.</param>
    /// <param name="assetDirectory">Directory containing avatar image assets.</param>
    /// <param name="logger">Optional logger.</param>
    public AvatarVideoStream(
        AvatarVideoGenerator generator,
        InteractiveAvatarService avatarService,
        IVisionModel? visionModel = null,
        VirtualSelf? virtualSelf = null,
        string? assetDirectory = null,
        ILogger<AvatarVideoStream>? logger = null)
    {
        _generator = generator;
        _avatarService = avatarService;
        _visionModel = visionModel;
        _virtualSelf = virtualSelf;
        _assetDirectory = assetDirectory ?? GetDefaultAssetDirectory();
        _logger = logger;
    }

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
        _logger?.LogInformation("Avatar video stream started (interval: {Interval}ms)", FrameIntervalMs);
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

        _logger?.LogInformation("Avatar video stream stopped");
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
                var state = _avatarService.CurrentState;
                var seedPath = AvatarVideoGenerator.GetSeedAssetPath(state.VisualState, _assetDirectory);

                if (!File.Exists(seedPath))
                {
                    _logger?.LogWarning("Seed asset not found: {Path}", seedPath);
                    await Task.Delay(FrameIntervalMs, ct);
                    continue;
                }

                byte[] seedBytes = await File.ReadAllBytesAsync(seedPath, ct);

                if (_visionModel != null)
                {
                    // ── Pure vision model path ──────────────────────────────────────
                    // Only re-analyse when the visual state actually changes — the seed
                    // image is static per state so calling on every tick is wasteful.
                    if (state.VisualState != lastAnalyzedState)
                    {
                        lastAnalyzedState = state.VisualState;
                        string expressionTarget = AvatarVideoGenerator.BuildPrompt(state);

                        // Vision model generates the expression description
                        var generated = await _visionModel.AnswerQuestionAsync(
                            seedBytes, "png",
                            $"Describe this face's expression in 8 words or less, focusing on: {expressionTarget}",
                            ct);

                        if (generated.IsSuccess)
                        {
                            string description = generated.Value.Trim();

                            // Push generated description as avatar status text
                            _avatarService.NotifyMoodChange(
                                mood: state.Mood ?? "neutral",
                                energy: 0.5,
                                positivity: 0.5,
                                statusText: description);

                            // Publish visual perception to VirtualSelf
                            _virtualSelf?.PublishVisualPerception(
                                description: description,
                                objects: Array.Empty<DetectedObject>(),
                                faces: Array.Empty<DetectedFace>(),
                                sceneType: "avatar",
                                emotion: state.Mood,
                                rawFrame: seedBytes);

                            _logger?.LogDebug("Vision model generated: {Description}", description);
                        }
                    }
                }
                else
                {
                    // ── SD fallback path (no vision model) ─────────────────────────
                    string seedBase64 = Convert.ToBase64String(seedBytes);
                    string prompt = AvatarVideoGenerator.BuildPrompt(state);
                    string? frameBase64 = await _generator.GenerateFrameAsync(prompt, seedBase64, ct);

                    if (frameBase64 != null)
                    {
                        byte[] frameBytes = Convert.FromBase64String(frameBase64);
                        await _avatarService.BroadcastVideoFrameAsync(frameBytes);
                        _virtualSelf?.PublishVisualPerception(
                            description: prompt,
                            objects: Array.Empty<DetectedObject>(),
                            faces: Array.Empty<DetectedFace>(),
                            sceneType: "avatar",
                            emotion: state.Mood,
                            rawFrame: frameBytes);
                    }
                }
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in avatar video generation loop");
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
