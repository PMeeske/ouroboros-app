// <copyright file="InteractiveAvatarService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Ouroboros.Application.Avatar;

/// <summary>
/// Core avatar orchestration service.
/// Observes presence and mood changes, resolves the visual state,
/// and broadcasts <see cref="AvatarStateSnapshot"/> to all attached renderers.
/// </summary>
public sealed class InteractiveAvatarService : IAsyncDisposable
{
    private readonly string _personaName;
    private readonly List<IAvatarRenderer> _renderers = new();
    private readonly BehaviorSubject<AvatarStateSnapshot> _state;
    private readonly CompositeDisposable _subscriptions = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractiveAvatarService"/> class.
    /// </summary>
    /// <param name="personaName">Name of the active persona (e.g. "Iaret").</param>
    public InteractiveAvatarService(string personaName)
    {
        _personaName = personaName;
        _state = new BehaviorSubject<AvatarStateSnapshot>(AvatarStateSnapshot.Default(personaName));
    }

    /// <summary>Gets the current avatar state as an observable stream.</summary>
    public IObservable<AvatarStateSnapshot> StateStream => _state.AsObservable();

    /// <summary>Gets the latest avatar state snapshot.</summary>
    public AvatarStateSnapshot CurrentState => _state.Value;

    /// <summary>
    /// Attaches a renderer that will receive all future state updates.
    /// </summary>
    public void AttachRenderer(IAvatarRenderer renderer)
    {
        _renderers.Add(renderer);
    }

    /// <summary>
    /// Broadcasts a raw JPEG video frame to all attached renderers that support binary frame streaming.
    /// Renderers must implement <see cref="IVideoFrameRenderer"/> to receive frames.
    /// </summary>
    /// <param name="jpegFrame">JPEG-encoded frame bytes.</param>
    public async Task BroadcastVideoFrameAsync(byte[] jpegFrame)
    {
        foreach (var renderer in _renderers.Where(r => r.IsActive && r is IVideoFrameRenderer))
        {
            try
            {
                await ((IVideoFrameRenderer)renderer).BroadcastFrameAsync(jpegFrame);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Individual renderer failures shouldn't crash the stream
            }
        }
    }

    /// <summary>
    /// Subscribes to a presence state observable (e.g. from <c>AgentPresenceController.State</c>).
    /// </summary>
    /// <param name="presenceStates">Observable of presence state names.</param>
    /// <param name="moodProvider">Function returning the current mood name.</param>
    /// <param name="energyProvider">Function returning the current energy level.</param>
    /// <param name="positivityProvider">Function returning the current positivity level.</param>
    public void BindPresence(
        IObservable<string> presenceStates,
        Func<string> moodProvider,
        Func<double> energyProvider,
        Func<double> positivityProvider)
    {
        var sub = presenceStates
            .DistinctUntilChanged()
            .Subscribe(presence =>
            {
                var mood = moodProvider();
                var visual = AvatarStateMapper.Resolve(presence, mood);
                PushState(new AvatarStateSnapshot(
                    visual, mood, energyProvider(), positivityProvider(), presence, _personaName, DateTime.UtcNow));
            });

        _subscriptions.Add(sub);
    }

    /// <summary>
    /// Directly updates the avatar with a new mood (e.g. from personality engine events).
    /// </summary>
    public void NotifyMoodChange(string mood, double energy, double positivity, string? statusText = null)
    {
        var currentPresence = _state.Value.StatusText ?? "Idle";
        var visual = AvatarStateMapper.Resolve(currentPresence, mood);
        PushState(new AvatarStateSnapshot(
            visual, mood, energy, positivity, statusText, _personaName, DateTime.UtcNow));
    }

    /// <summary>
    /// Updates the avatar's topic hint and optionally the status text, keeping all other state unchanged.
    /// The HTML viewer uses the topic to shift stage position and trigger a micro-expression flash.
    /// </summary>
    public void SetTopicHint(string topic, string? statusText = null)
    {
        var current = _state.Value;
        PushState(current with
        {
            Topic = topic,
            StatusText = statusText ?? current.StatusText,
            Timestamp = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// Directly sets the presence state (for callers without an Rx stream).
    /// </summary>
    public void SetPresenceState(string presenceState, string mood, double energy = 0.5, double positivity = 0.5)
    {
        var visual = AvatarStateMapper.Resolve(presenceState, mood);
        PushState(new AvatarStateSnapshot(
            visual, mood, energy, positivity, presenceState, _personaName, DateTime.UtcNow));
    }

    /// <summary>
    /// Starts all attached renderers.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        foreach (var renderer in _renderers)
        {
            await renderer.StartAsync(ct);
        }

        // Forward state changes to all active renderers
        var renderSub = _state
            .DistinctUntilChanged()
            .Subscribe(async snapshot =>
            {
                foreach (var renderer in _renderers.Where(r => r.IsActive))
                {
                    try
                    {
                        await renderer.UpdateStateAsync(snapshot);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Individual renderer failures shouldn't crash the system
                    }
                }
            });

        _subscriptions.Add(renderSub);
    }

    /// <summary>
    /// Stops all renderers and disposes resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _subscriptions.Dispose();

        foreach (var renderer in _renderers)
        {
            try
            {
                await renderer.StopAsync();
                await renderer.DisposeAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort cleanup
            }
        }

        _state.Dispose();
    }

    private void PushState(AvatarStateSnapshot snapshot)
    {
        if (!_disposed)
        {
            _state.OnNext(snapshot);
        }
    }
}
