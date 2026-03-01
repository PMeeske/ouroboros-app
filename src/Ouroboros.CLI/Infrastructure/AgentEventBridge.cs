// Copyright (c) Ouroboros. All rights reserved.

using MediatR;
using Ouroboros.Application.Extensions;
using Ouroboros.Application.Integration;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Mediator.Notifications;
using Ouroboros.CLI.Services.RoomPresence;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Bridges existing domain events (PresenceDetector, RoomIntentBus, ImmersivePersona,
/// EventBus, EventBroker, etc.) into the MediatR notification pipeline so that any
/// <see cref="INotificationHandler{T}"/> — including the agent's own reaction handler —
/// can subscribe declaratively.
///
/// Call the <c>Wire*</c> methods during agent initialization (Phase 8 cross-wiring)
/// after all subsystems are up.
/// </summary>
public sealed class AgentEventBridge : IDisposable
{
    private readonly IMediator _mediator;
    private readonly List<IDisposable> _subscriptions = new();
    private bool _disposed;

    public AgentEventBridge(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Presence
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Subscribes to <see cref="PresenceDetector.OnPresenceDetected"/> and
    /// <see cref="PresenceDetector.OnAbsenceDetected"/>, publishing
    /// <see cref="PresenceChangedNotification"/> for each.
    /// </summary>
    public void WirePresenceDetector(PresenceDetector detector)
    {
        detector.OnPresenceDetected += OnPresenceEvent;
        detector.OnAbsenceDetected += OnPresenceEvent;
    }

    private void OnPresenceEvent(PresenceEvent evt)
    {
        _ = PublishSafe(new PresenceChangedNotification(evt));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Room / Voice  (static events on RoomIntentBus)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Subscribes to <see cref="RoomIntentBus"/> static events and publishes
    /// <see cref="RoomUtteranceNotification"/> / <see cref="SpeakerIdentifiedNotification"/>.
    /// </summary>
    public void WireRoomIntentBus()
    {
        RoomIntentBus.OnIaretInterjected += OnIaretInterjected;
        RoomIntentBus.OnUserAddressedIaret += OnUserAddressedIaret;
        RoomIntentBus.OnSpeakerIdentified += OnSpeakerIdentified;
    }

    private void OnIaretInterjected(string personaName, string speech)
    {
        // Iaret's own speech — other handlers may want to know (e.g., logging).
        _ = PublishSafe(new RoomUtteranceNotification(personaName, speech, IsAddressingAgent: false));
    }

    private void OnUserAddressedIaret(string speaker, string utterance)
    {
        _ = PublishSafe(new RoomUtteranceNotification(speaker, utterance, IsAddressingAgent: true));
    }

    private void OnSpeakerIdentified(string speakerLabel, bool isOwner)
    {
        _ = PublishSafe(new SpeakerIdentifiedNotification(speakerLabel, isOwner));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ImmersivePersona events
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Subscribes to <see cref="ImmersivePersona.ConsciousnessShift"/> and
    /// <see cref="ImmersivePersona.AutonomousThought"/> events.
    /// </summary>
    public void WirePersona(ImmersivePersona persona)
    {
        persona.ConsciousnessShift += OnConsciousnessShift;
        persona.AutonomousThought += OnAutonomousThought;
    }

    private void OnConsciousnessShift(object? sender, ConsciousnessShiftEventArgs e)
    {
        _ = PublishSafe(new ConsciousnessShiftedNotification(e.NewEmotion, e.ArousalChange, e.NewState));
    }

    private void OnAutonomousThought(object? sender, AutonomousThoughtEventArgs e)
    {
        _ = PublishSafe(new AutonomousThoughtNotification(e.Thought));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Application-level EventBus (Rx)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Subscribes to the <see cref="IEventBus"/> Rx streams and publishes
    /// corresponding MediatR notifications for each known <see cref="SystemEvent"/> type.
    /// </summary>
    public void WireEventBus(IEventBus eventBus)
    {
        _subscriptions.Add(
            eventBus.Subscribe<GoalExecutedEvent>().Subscribe(e =>
                _ = PublishSafe(new GoalExecutedNotification(e.Goal, e.Success, e.Duration))));

        _subscriptions.Add(
            eventBus.Subscribe<LearningCompletedEvent>().Subscribe(e =>
                _ = PublishSafe(new LearningCompletedNotification(e.EpisodesProcessed, e.RulesLearned))));

        _subscriptions.Add(
            eventBus.Subscribe<ReasoningCompletedEvent>().Subscribe(e =>
                _ = PublishSafe(new ReasoningCompletedNotification(e.Query, e.Answer, e.Confidence))));

        _subscriptions.Add(
            eventBus.Subscribe<ConsciousnessStateChangedEvent>().Subscribe(e =>
                _ = PublishSafe(new ConsciousnessShiftedNotification(
                    e.NewState, 0.0,
                    new ConsciousnessState(
                        CurrentFocus: "consciousness_shift",
                        Arousal: 0.5,
                        Valence: 0.0,
                        ActiveDrives: new Dictionary<string, double>(),
                        ActiveAssociations: e.ActiveItems,
                        DominantEmotion: e.NewState,
                        Awareness: 0.6,
                        AttentionalSpotlight: Array.Empty<string>(),
                        StateTimestamp: e.Timestamp)))));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CLI EventBroker<AgentEvent> (Channels)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Subscribes to the CLI-level <see cref="EventBroker{T}"/> for
    /// <see cref="AgentEvent"/> and publishes tool-related MediatR notifications.
    /// </summary>
    public void WireAgentEventBroker(EventBroker<AgentEvent> broker, CancellationToken ct)
    {
        var reader = broker.Subscribe(ct);
        Task.Run(async () =>
        {
            await foreach (var evt in reader.ReadAllAsync(ct))
            {
                switch (evt)
                {
                    case ToolStartedEvent ts:
                        _ = PublishSafe(new ToolStartedNotification(ts.ToolName, ts.Param));
                        break;
                    case ToolCompletedEvent tc:
                        _ = PublishSafe(new ToolCompletedNotification(tc.ToolName, tc.Success, tc.Output, tc.Elapsed));
                        break;
                    // AgentThinkingEvent and AgentResponseEvent are internal UI concerns;
                    // skip forwarding unless there's a reason to expose them.
                }
            }
        }, ct)
        .ObserveExceptions("AgentEventBroker reader");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Exception routing — all exceptions pass through Iaret's kernel
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wires global exception handlers so every exception — fire-and-forget faults,
    /// unhandled domain exceptions, and unobserved task exceptions — routes through
    /// Iaret's consciousness via <see cref="ExceptionSink"/>.
    /// Call once during agent initialization.
    /// </summary>
    public static void WireExceptionRouting(IAgentEventSink sink)
    {
        ExceptionSink.SetSink(sink);

        // Fire-and-forget task faults (via ObserveExceptions)
        Application.Extensions.TaskExtensions.ExceptionObserved += (ex, context) =>
            ExceptionSink.Publish(ex, context ?? "fire-and-forget", isFatal: false);

        // Unhandled exceptions on any thread
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                ExceptionSink.Publish(ex, "unhandled", isFatal: args.IsTerminating);
        };

        // Unobserved task exceptions (tasks that faulted without anyone awaiting)
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            if (args.Exception != null)
            {
                ExceptionSink.Publish(args.Exception, "unobserved-task", isFatal: false);
                args.SetObserved(); // Prevent process crash
            }
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Device events (Tapo cameras, generic IoT)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Publishes a device event notification.  Call this from any subsystem
    /// that detects device activity (e.g., Tapo motion detection, sensor readings).
    /// </summary>
    public void PublishDeviceEvent(string deviceType, string deviceId, string eventKind, string? payload = null)
    {
        _ = PublishSafe(new DeviceEventNotification(deviceType, deviceId, eventKind, payload));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Publishes a notification swallowing exceptions so event producers are
    /// never blocked by a failing handler.
    /// </summary>
    private async Task PublishSafe<T>(T notification) where T : INotification
    {
        try
        {
            await _mediator.Publish(notification);
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AgentEventBridge] Failed to publish {typeof(T).Name}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe static events
        RoomIntentBus.OnIaretInterjected -= OnIaretInterjected;
        RoomIntentBus.OnUserAddressedIaret -= OnUserAddressedIaret;
        RoomIntentBus.OnSpeakerIdentified -= OnSpeakerIdentified;

        // Dispose Rx subscriptions
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();

        ExceptionSink.Clear();
    }
}
