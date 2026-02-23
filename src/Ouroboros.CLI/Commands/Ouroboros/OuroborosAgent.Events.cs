// Copyright (c) Ouroboros. All rights reserved.

using System.Threading.Channels;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Mediator.Notifications;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Event sink &amp; proactive reaction loop for the Main Agent (Iaret).
///
/// All MediatR agent-event notifications are funnelled into a bounded channel
/// via <see cref="IAgentEventSink.Enqueue"/>.  A background loop drains the
/// channel, records each event in the Hyperon AtomSpace, and — when conditions
/// are met — triggers a proactive response (voice, thought, or console output).
/// </summary>
public sealed partial class OuroborosAgent : IAgentEventSink
{
    // ── Event sink channel ──────────────────────────────────────────────────
    private readonly Channel<AgentEventNotification> _eventChannel =
        Channel.CreateBounded<AgentEventNotification>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    private CancellationTokenSource? _eventLoopCts;
    private Task? _eventLoopTask;

    /// <inheritdoc/>
    void IAgentEventSink.Enqueue(AgentEventNotification notification)
    {
        // Non-blocking write; drops oldest if full.
        _eventChannel.Writer.TryWrite(notification);
    }

    /// <inheritdoc/>
    int IAgentEventSink.PendingCount => _eventChannel.Reader.Count;

    // ── AgentEventBridge reference (set during Init) ────────────────────────
    private AgentEventBridge? _agentEventBridge;

    /// <summary>
    /// Exposes the bridge so handlers or subsystems can publish device events directly.
    /// </summary>
    internal AgentEventBridge? EventBridge => _agentEventBridge;

    // ── Background event loop ───────────────────────────────────────────────

    /// <summary>
    /// Starts the background event-processing loop.
    /// Call once during initialization after all subsystems are wired.
    /// </summary>
    internal void StartEventLoop()
    {
        _eventLoopCts?.Cancel();
        _eventLoopCts?.Dispose();
        _eventLoopCts = new CancellationTokenSource();
        _eventLoopTask = Task.Run(() => EventLoopAsync(_eventLoopCts.Token), _eventLoopCts.Token);
    }

    /// <summary>
    /// Stops the background event-processing loop (called during disposal).
    /// </summary>
    internal async Task StopEventLoopAsync()
    {
        _eventLoopCts?.Cancel();
        if (_eventLoopTask != null)
        {
            try { await _eventLoopTask; }
            catch (OperationCanceledException) { }
        }
        _eventLoopCts?.Dispose();
        _eventLoopCts = null;

        _agentEventBridge?.Dispose();
        _agentEventBridge = null;
    }

    // ── Core event loop ─────────────────────────────────────────────────────

    private async Task EventLoopAsync(CancellationToken ct)
    {
        await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await ProcessAgentEventAsync(evt, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentEvents] Error processing {evt.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Central dispatch for every agent event.  Records in the Hyperon AtomSpace
    /// for symbolic reasoning, updates world state, and optionally triggers
    /// proactive behaviour (voice, console, thought).
    /// </summary>
    private async Task ProcessAgentEventAsync(AgentEventNotification evt, CancellationToken ct)
    {
        // ── 1. Record in Hyperon AtomSpace (symbolic memory) ────────────
        await RecordEventInHyperonAsync(evt, ct);

        // ── 2. Dispatch to type-specific reaction logic ─────────────────
        switch (evt)
        {
            case PresenceChangedNotification presence:
                await OnPresenceChangedAsync(presence, ct);
                break;

            case RoomUtteranceNotification utterance:
                await OnRoomUtteranceAsync(utterance, ct);
                break;

            case SpeakerIdentifiedNotification speaker:
                OnSpeakerIdentified(speaker);
                break;

            case DeviceEventNotification device:
                await OnDeviceEventAsync(device, ct);
                break;

            case ConsciousnessShiftedNotification shift:
                OnConsciousnessShifted(shift);
                break;

            case GoalExecutedNotification goal:
                OnGoalExecuted(goal);
                break;

            // Other events are recorded in Hyperon but don't require
            // additional proactive behaviour by default.
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Hyperon recording — every event becomes a symbolic atom
    // ═══════════════════════════════════════════════════════════════════════

    private async Task RecordEventInHyperonAsync(AgentEventNotification evt, CancellationToken ct)
    {
        var persona = _immersivePersona;
        if (persona?.HyperonFlow == null) return;

        var engine = persona.HyperonFlow.Engine;
        var ticks = evt.Timestamp.Ticks.ToString();

        var fact = evt switch
        {
            PresenceChangedNotification p =>
                $"(AgentEvent presence {p.Event.State} {p.Event.Confidence:F2} {ticks})",

            RoomUtteranceNotification r =>
                $"(AgentEvent room-utterance \"{Sanitize(r.Speaker)}\" \"{Sanitize(r.Text)}\" {ticks})",

            SpeakerIdentifiedNotification s =>
                $"(AgentEvent speaker-id \"{Sanitize(s.SpeakerLabel)}\" {(s.IsOwner ? "owner" : "guest")} {ticks})",

            DeviceEventNotification d =>
                $"(AgentEvent device \"{Sanitize(d.DeviceType)}\" \"{Sanitize(d.DeviceId)}\" \"{Sanitize(d.EventKind)}\" {ticks})",

            ConsciousnessShiftedNotification c =>
                $"(AgentEvent consciousness-shift \"{Sanitize(c.NewEmotion)}\" {c.ArousalChange:F2} {ticks})",

            AutonomousThoughtNotification t =>
                $"(AgentEvent thought \"{Sanitize(t.Thought.Content)}\" {ticks})",

            ToolStartedNotification ts =>
                $"(AgentEvent tool-start \"{Sanitize(ts.ToolName)}\" {ticks})",

            ToolCompletedNotification tc =>
                $"(AgentEvent tool-done \"{Sanitize(tc.ToolName)}\" {(tc.Success ? "ok" : "fail")} {ticks})",

            GoalExecutedNotification g =>
                $"(AgentEvent goal \"{Sanitize(g.Goal)}\" {(g.Success ? "ok" : "fail")} {ticks})",

            LearningCompletedNotification l =>
                $"(AgentEvent learning {l.EpisodesProcessed} {l.RulesLearned} {ticks})",

            ReasoningCompletedNotification rc =>
                $"(AgentEvent reasoning \"{Sanitize(rc.Query)}\" {rc.Confidence:F2} {ticks})",

            _ => $"(AgentEvent unknown \"{evt.Source}\" {ticks})"
        };

        try
        {
            await engine.AddFactAsync(fact, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AgentEvents] Hyperon record failed: {ex.Message}");
        }
    }

    private static string Sanitize(string? s) =>
        (s ?? "").Replace("\"", "'").Replace("\n", " ").Replace("\r", "");

    // ═══════════════════════════════════════════════════════════════════════
    // Type-specific reaction logic
    // ═══════════════════════════════════════════════════════════════════════

    private async Task OnPresenceChangedAsync(PresenceChangedNotification n, CancellationToken ct)
    {
        // Delegate to existing HandlePresenceRequest for backwards-compat greeting logic.
        // The bridge now fires this in parallel, so no double-greeting risk:
        // the original WirePresenceDetection path will be removed after bridge wiring.
        if (n.Event.State == PresenceState.Present)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AgentEvents] Presence detected ({n.Event.Source}, confidence={n.Event.Confidence:P0})");
        }
        else
        {
            _userWasPresent = false;
            System.Diagnostics.Debug.WriteLine(
                $"[AgentEvents] Absence detected via {n.Event.Source}");
        }

        await Task.CompletedTask;
    }

    private async Task OnRoomUtteranceAsync(RoomUtteranceNotification n, CancellationToken ct)
    {
        if (!n.IsAddressingAgent) return;

        System.Diagnostics.Debug.WriteLine(
            $"[AgentEvents] Room: {n.Speaker} addressed Iaret: \"{n.Text}\"");

        // Record as a conversational memory fragment in the persona
        _immersivePersona?.UpdateInnerDialogContext(n.Text);
        await Task.CompletedTask;
    }

    private void OnSpeakerIdentified(SpeakerIdentifiedNotification n)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[AgentEvents] Speaker identified: {n.SpeakerLabel} (owner={n.IsOwner})");
    }

    private async Task OnDeviceEventAsync(DeviceEventNotification n, CancellationToken ct)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[AgentEvents] Device [{n.DeviceType}/{n.DeviceId}] {n.EventKind}: {n.Payload}");

        // For significant device events, consider triggering a proactive thought
        if (n.EventKind is "motion_detected" or "alarm" or "person_detected")
        {
            var thoughtContent = $"Detected {n.EventKind} on {n.DeviceType} device {n.DeviceId}";
            if (!string.IsNullOrEmpty(n.Payload))
                thoughtContent += $": {n.Payload}";

            if (_config.Verbosity != OutputVerbosity.Quiet)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"\n  [device] {thoughtContent}");
                Console.ResetColor();
            }

            // Let the persona think about it
            _immersivePersona?.UpdateInnerDialogContext(thoughtContent);
        }

        await Task.CompletedTask;
    }

    private void OnConsciousnessShifted(ConsciousnessShiftedNotification n)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[AgentEvents] Consciousness shift: {n.NewEmotion} (arousal delta={n.ArousalChange:+0.00;-0.00})");
    }

    private void OnGoalExecuted(GoalExecutedNotification n)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[AgentEvents] Goal executed: {n.Goal} (success={n.Success}, duration={n.Duration})");
    }
}
