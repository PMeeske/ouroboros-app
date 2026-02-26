// <copyright file="CognitiveStreamEngine.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Streams;

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Ouroboros.Agent.MetaAI.Affect;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Domain.Autonomous;

/// <summary>
/// Permanently-running Rx cognitive stream engine.
///
/// Aggregates ALL cognitive event sources into one merged <see cref="IObservable{CognitiveEvent}"/>.
/// Does NOT rewrite any existing loops — it bridges their .NET events via <see cref="Subject{T}"/>
/// and adds interval-based pulse generators (valence every 30 s, personality every 60 s).
///
/// Two consumers:
///   1. Throttled console display — color-coded per <see cref="StreamKind"/>.
///   2. <see cref="ReplaySubject{T}"/> buffer (50 events / 5-min window) →
///      <see cref="BuildContextBlock"/> for LLM context injection on each chat turn.
/// </summary>
public sealed class CognitiveStreamEngine : IDisposable
{
    // ── One subject per cognitive source domain ────────────────────────────
    private readonly Subject<CognitiveEvent> _thoughtSubject       = new();
    private readonly Subject<CognitiveEvent> _discoverySubject     = new();
    private readonly Subject<CognitiveEvent> _emotionSubject       = new();
    private readonly Subject<CognitiveEvent> _actionSubject        = new();
    private readonly Subject<CognitiveEvent> _actionEngineSubject  = new();
    private readonly Subject<CognitiveEvent> _innerDialogSubject   = new();
    private readonly Subject<CognitiveEvent> _consciousnessSubject = new();
    private readonly Subject<CognitiveEvent> _interactionSubject   = new();
    private readonly Subject<CognitiveEvent> _coordinatorSubject   = new();

    // ── Replay buffer: last 50 events within 5-minute window ──────────────
    private readonly ReplaySubject<CognitiveEvent> _replayBuffer =
        new(bufferSize: 50, window: TimeSpan.FromMinutes(5));

    // ── Merged public stream ───────────────────────────────────────────────
    /// <summary>
    /// Single merged observable of all cognitive events.
    /// Subscribe for reactive processing. Display and context use the internal buffer.
    /// </summary>
    public IObservable<CognitiveEvent> Stream { get; }

    private readonly CompositeDisposable _disposables = new();

    // ── Display throttle: last display time per kind ───────────────────────
    private readonly Dictionary<StreamKind, DateTime> _lastDisplayTime = new();

    // Minimum gap between console renders per stream kind.
    // Zero = never render to console (context-only stream).
    private static readonly IReadOnlyDictionary<StreamKind, TimeSpan> DisplayGaps =
        new Dictionary<StreamKind, TimeSpan>
        {
            [StreamKind.Thought]            = TimeSpan.FromSeconds(8),
            [StreamKind.Discovery]          = TimeSpan.FromSeconds(5),
            [StreamKind.EmotionalChange]    = TimeSpan.FromSeconds(15),
            [StreamKind.AutonomousAction]   = TimeSpan.FromSeconds(10),
            [StreamKind.ActionEngine]       = TimeSpan.FromSeconds(1),
            [StreamKind.InnerDialog]        = TimeSpan.FromSeconds(12),
            [StreamKind.ConsciousnessShift] = TimeSpan.FromSeconds(20),
            [StreamKind.ValencePulse]       = TimeSpan.Zero,
            [StreamKind.PersonalityPulse]   = TimeSpan.Zero,
            [StreamKind.UserInteraction]    = TimeSpan.Zero,
            [StreamKind.CoordinatorMessage] = TimeSpan.FromSeconds(5),
        };

    private bool _disposed;

    public CognitiveStreamEngine()
    {
        // Merge all subjects with .Synchronize() for thread-safety
        Stream = Observable.Merge(
                _thoughtSubject.Synchronize(),
                _discoverySubject.Synchronize(),
                _emotionSubject.Synchronize(),
                _actionSubject.Synchronize(),
                _actionEngineSubject.Synchronize(),
                _innerDialogSubject.Synchronize(),
                _consciousnessSubject.Synchronize(),
                _interactionSubject.Synchronize(),
                _coordinatorSubject.Synchronize())
            .Publish()
            .RefCount();

        // Pipe all events into the replay buffer (always, no throttle)
        _disposables.Add(Stream.Subscribe(_replayBuffer));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Interval-based pulse generators (call once after monitored objects ready)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts a 30-second interval pulse that samples <see cref="IValenceMonitor.GetCurrentState"/>.
    /// Events go directly to the replay buffer (never displayed).
    /// </summary>
    public void StartValencePulse(IValenceMonitor valenceMonitor)
    {
        var sub = Observable
            .Interval(TimeSpan.FromSeconds(30), TaskPoolScheduler.Default)
            .Select(_ => (CognitiveEvent)new ValencePulseEvent(
                valenceMonitor.GetCurrentState(), DateTime.UtcNow))
            .Subscribe(_replayBuffer);
        _disposables.Add(sub);
    }

    /// <summary>
    /// Starts a 60-second interval pulse that samples the current <see cref="PersonalityProfile"/>.
    /// Events go directly to the replay buffer (never displayed).
    /// </summary>
    public void StartPersonalityPulse(Func<PersonalityProfile?> getProfile)
    {
        var sub = Observable
            .Interval(TimeSpan.FromSeconds(60), TaskPoolScheduler.Default)
            .Select(_ => getProfile())
            .Where(p => p is not null)
            .Select(p =>
            {
                var traits = string.Join(", ",
                    p!.GetActiveTraits(3).Select(t => $"{t.Name}({t.EffectiveIntensity:F2})"));
                var mood = p.CurrentMood?.Name ?? "neutral";
                return (CognitiveEvent)new PersonalityPulseEvent(
                    p.PersonaName, traits, mood, DateTime.UtcNow);
            })
            .Subscribe(_replayBuffer);
        _disposables.Add(sub);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Manual emit — called from OuroborosAgent.Init.cs event bridges
    // ═══════════════════════════════════════════════════════════════════════

    public void EmitThought(Thought thought) =>
        _thoughtSubject.OnNext(new ThoughtEvent(thought, DateTime.UtcNow));

    public void EmitDiscovery(string query, string fact) =>
        _discoverySubject.OnNext(new DiscoveryEvent(query, fact, DateTime.UtcNow));

    public void EmitEmotionalChange(EmotionalState state) =>
        _emotionSubject.OnNext(new EmotionalChangeEvent(state, DateTime.UtcNow));

    public void EmitAutonomousAction(AutonomousAction action) =>
        _actionSubject.OnNext(new AutonomousActionEvent(action, DateTime.UtcNow));

    public void EmitActionEngine(string reason, string result) =>
        _actionEngineSubject.OnNext(new ActionEngineEvent(reason, result, DateTime.UtcNow));

    public void EmitInnerDialog(InnerThought thought) =>
        _innerDialogSubject.OnNext(new InnerDialogEvent(thought, DateTime.UtcNow));

    public void EmitConsciousnessShift(string? emotion, double arousalChange) =>
        _consciousnessSubject.OnNext(new ConsciousnessShiftEvent(emotion, arousalChange, DateTime.UtcNow));

    public void EmitUserInteraction(string input) =>
        _interactionSubject.OnNext(new UserInteractionEvent(input, DateTime.UtcNow));

    public void EmitCoordinatorMessage(ProactiveMessageEventArgs msg) =>
        _coordinatorSubject.OnNext(new CoordinatorMessageEvent(msg, DateTime.UtcNow));

    /// <summary>
    /// Emits a raw thought using <see cref="ThoughtType.Reflection"/> type.
    /// Used by OuroborosMeTTaTool atom event hooks (OnSelfConsumption, OnFixedPoint).
    /// </summary>
    public void EmitRawThought(string content)
        => EmitThought(new Thought { Type = ThoughtType.Reflection, Content = content, Timestamp = DateTime.UtcNow });

    /// <summary>
    /// Emits a tool execution result as an <see cref="ActionEngineEvent"/> into the cognitive stream.
    /// Call after <c>GenerateWithToolsAsync</c> for interesting tool names.
    /// </summary>
    public void EmitToolExecution(string toolName, string output)
    {
        var truncated = output.Length > 120 ? output[..120] + "…" : output;
        _actionEngineSubject.OnNext(new ActionEngineEvent($"[{toolName}]", truncated, DateTime.UtcNow));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Console display (throttled, color-coded per StreamKind)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts the throttled console display subscription.
    /// Call once during initialization. Pass <c>true</c> for quiet mode to suppress output.
    /// </summary>
    public void StartConsoleDisplay(bool isQuiet = false)
    {
        if (isQuiet) return;

        var sub = Stream
            .ObserveOn(TaskPoolScheduler.Default)
            .Where(ShouldDisplay)
            .Subscribe(evt =>
            {
                try { RenderToConsole(evt); }
                catch { /* never crash the stream due to display errors */ }
            });
        _disposables.Add(sub);
    }

    private bool ShouldDisplay(CognitiveEvent evt)
    {
        if (!DisplayGaps.TryGetValue(evt.Kind, out var gap) || gap == TimeSpan.Zero)
            return false;

        lock (_lastDisplayTime)
        {
            if (_lastDisplayTime.TryGetValue(evt.Kind, out var last) &&
                DateTime.UtcNow - last < gap)
                return false;
            _lastDisplayTime[evt.Kind] = DateTime.UtcNow;
            return true;
        }
    }

    private static void RenderToConsole(CognitiveEvent evt)
    {
        var (ansi, icon) = evt.Kind switch
        {
            StreamKind.Thought            => ("\x1b[38;2;128;0;180m",  "💭"),
            StreamKind.Discovery          => ("\x1b[38;2;0;180;180m",  "💡"),
            StreamKind.EmotionalChange    => ("\x1b[38;2;220;120;0m",  "❤"),
            StreamKind.AutonomousAction   => ("\x1b[38;2;0;160;80m",   "⚙"),
            StreamKind.ActionEngine       => ("\x1b[38;2;0;200;160m",  "🤖"),
            StreamKind.InnerDialog        => ("\x1b[38;2;100;60;200m", "💬"),
            StreamKind.ConsciousnessShift => ("\x1b[38;2;200;0;100m",  "✦"),
            StreamKind.CoordinatorMessage => ("\x1b[38;2;200;160;0m",  "📡"),
            _                             => ("\x1b[90m",               "•"),
        };

        Console.WriteLine($"\n  {ansi}{icon} [stream] {evt.Summary}\x1b[0m");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LLM context block (synchronous snapshot from ReplaySubject)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds a context block injected into every LLM prompt.
    /// Reads the current replay buffer synchronously (ReplaySubject delivers
    /// buffered values synchronously on Subscribe). Returns empty string if no events.
    /// </summary>
    public string BuildContextBlock()
    {
        var buffered = new List<CognitiveEvent>();
        using var _ = _replayBuffer.Subscribe(e => buffered.Add(e));

        if (buffered.Count == 0) return string.Empty;

        // Most recent event per kind
        var byKind = buffered
            .GroupBy(e => e.Kind)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Timestamp).First());

        var sb = new StringBuilder();
        sb.AppendLine("\n[COGNITIVE STREAM — live background state]");

        if (byKind.TryGetValue(StreamKind.ValencePulse, out var vRaw) &&
            vRaw is ValencePulseEvent vp)
        {
            sb.AppendLine($"  Affect: valence={vp.State.Valence:+0.00;-0.00} " +
                          $"stress={vp.State.Stress:F2} " +
                          $"curiosity={vp.State.Curiosity:F2} " +
                          $"arousal={vp.State.Arousal:F2}");
        }

        if (byKind.TryGetValue(StreamKind.PersonalityPulse, out var pRaw) &&
            pRaw is PersonalityPulseEvent pp)
        {
            sb.AppendLine($"  Traits: {pp.TopTraits} | mood: {pp.Mood}");
        }

        if (byKind.TryGetValue(StreamKind.Thought, out var tRaw) &&
            tRaw is ThoughtEvent te)
        {
            sb.AppendLine($"  Recent thought ({Age(te.Timestamp)}): [{te.Thought.Type}] " +
                          $"{Clip(te.Thought.Content, 120)}");
        }

        if (byKind.TryGetValue(StreamKind.Discovery, out var dRaw) &&
            dRaw is DiscoveryEvent de)
        {
            sb.AppendLine($"  Discovery ({Age(de.Timestamp)}): {Clip(de.Fact, 130)}");
        }

        if (byKind.TryGetValue(StreamKind.ActionEngine, out var aRaw) &&
            aRaw is ActionEngineEvent ae)
        {
            sb.AppendLine($"  Last action ({Age(ae.Timestamp)}): {Clip(ae.Reason, 110)}");
        }

        if (byKind.TryGetValue(StreamKind.EmotionalChange, out var eRaw) &&
            eRaw is EmotionalChangeEvent ec)
        {
            sb.AppendLine($"  Emotional state: {ec.State.Description} ({ec.State.DominantEmotion})");
        }

        if (byKind.TryGetValue(StreamKind.InnerDialog, out var iRaw) &&
            iRaw is InnerDialogEvent ide)
        {
            sb.AppendLine($"  Inner dialog ({Age(ide.Timestamp)}): [{ide.Thought.Type}] " +
                          $"{Clip(ide.Thought.Content, 110)}");
        }

        sb.AppendLine("[END COGNITIVE STREAM]\n");
        return sb.ToString();
    }

    private static string Age(DateTime ts)
    {
        var d = DateTime.UtcNow - ts;
        return d.TotalMinutes < 1 ? $"{d.TotalSeconds:F0}s ago"
             : d.TotalHours   < 1 ? $"{d.TotalMinutes:F0}m ago"
             : $"{d.TotalHours:F0}h ago";
    }

    private static string Clip(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    // ═══════════════════════════════════════════════════════════════════════
    // Disposal
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposables.Dispose();
        _thoughtSubject.OnCompleted();       _thoughtSubject.Dispose();
        _discoverySubject.OnCompleted();     _discoverySubject.Dispose();
        _emotionSubject.OnCompleted();       _emotionSubject.Dispose();
        _actionSubject.OnCompleted();        _actionSubject.Dispose();
        _actionEngineSubject.OnCompleted();  _actionEngineSubject.Dispose();
        _innerDialogSubject.OnCompleted();   _innerDialogSubject.Dispose();
        _consciousnessSubject.OnCompleted(); _consciousnessSubject.Dispose();
        _interactionSubject.OnCompleted();   _interactionSubject.Dispose();
        _coordinatorSubject.OnCompleted();   _coordinatorSubject.Dispose();
        _replayBuffer.Dispose();
    }
}
