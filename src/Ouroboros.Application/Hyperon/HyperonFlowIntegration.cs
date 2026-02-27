// <copyright file="HyperonFlowIntegration.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Hyperon;

using System.Collections.Concurrent;
using System.Threading.Channels;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.Hyperon.Parsing;

/// <summary>
/// Integrates the Hyperon AtomSpace with Ouroboros flows for neural-symbolic reasoning.
/// Provides reactive atom streams, pattern-triggered callbacks, and flow composition.
/// </summary>
public sealed class HyperonFlowIntegration : IAsyncDisposable
{
    private readonly HyperonMeTTaEngine _engine;
    private readonly Channel<HyperonFlowEvent> _eventChannel;
    private readonly ConcurrentDictionary<string, PatternSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, HyperonFlow> _flows = new();
    private readonly SExpressionParser _parser = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>
    /// Gets the underlying Hyperon engine.
    /// </summary>
    public HyperonMeTTaEngine Engine => _engine;

    /// <summary>
    /// Event raised when a subscribed pattern matches.
    /// </summary>
    public event Action<PatternMatchEvent>? OnPatternMatch;

    /// <summary>
    /// Event raised when a flow completes.
    /// </summary>
#pragma warning disable CS0067 // Event is never used - public API for external subscribers
    public event Action<FlowCompletionEvent>? OnFlowComplete;
#pragma warning restore CS0067

    /// <summary>
    /// Initializes a new instance of the <see cref="HyperonFlowIntegration"/> class.
    /// </summary>
    /// <param name="engine">Optional existing engine to use.</param>
    public HyperonFlowIntegration(HyperonMeTTaEngine? engine = null)
    {
        _engine = engine ?? new HyperonMeTTaEngine();
        _eventChannel = Channel.CreateUnbounded<HyperonFlowEvent>();

        // Subscribe to atom additions
        _engine.AtomAdded += OnAtomAdded;

        // Start event processing
        _ = ProcessEventsAsync(_cts.Token);
    }

    /// <summary>
    /// Creates a new Hyperon flow with the given name.
    /// </summary>
    /// <param name="flowName">Unique name for the flow.</param>
    /// <param name="description">Human-readable description.</param>
    /// <returns>The created flow.</returns>
    public HyperonFlow CreateFlow(string flowName, string? description = null)
    {
        var flow = new HyperonFlow(flowName, _engine, description);
        _flows[flowName] = flow;
        return flow;
    }

    /// <summary>
    /// Gets an existing flow by name.
    /// </summary>
    /// <param name="flowName">The flow name.</param>
    /// <returns>The flow if found.</returns>
    public Option<HyperonFlow> GetFlow(string flowName)
    {
        return _flows.TryGetValue(flowName, out var flow)
            ? Option<HyperonFlow>.Some(flow)
            : Option<HyperonFlow>.None();
    }

    /// <summary>
    /// Subscribes to a pattern - callback is invoked when matching atoms are added.
    /// </summary>
    /// <param name="subscriptionId">Unique ID for this subscription.</param>
    /// <param name="pattern">MeTTa pattern to match (can contain variables).</param>
    /// <param name="callback">Callback invoked on match.</param>
    /// <returns>True if subscription was created.</returns>
    public bool SubscribePattern(string subscriptionId, string pattern, Action<PatternMatchEvent> callback)
    {
        var parseResult = _parser.Parse(pattern);
        if (!parseResult.IsSuccess)
            return false;

        var subscription = new PatternSubscription(subscriptionId, parseResult.Value, callback);
        return _subscriptions.TryAdd(subscriptionId, subscription);
    }

    /// <summary>
    /// Subscribes to a pattern using a pre-parsed atom.
    /// </summary>
    /// <param name="subscriptionId">Unique ID for this subscription.</param>
    /// <param name="pattern">Pattern atom to match.</param>
    /// <param name="callback">Callback invoked on match.</param>
    /// <returns>True if subscription was created.</returns>
    public bool SubscribePattern(string subscriptionId, Atom pattern, Action<PatternMatchEvent> callback)
    {
        var subscription = new PatternSubscription(subscriptionId, pattern, callback);
        return _subscriptions.TryAdd(subscriptionId, subscription);
    }

    /// <summary>
    /// Unsubscribes from a pattern.
    /// </summary>
    /// <param name="subscriptionId">The subscription ID to remove.</param>
    /// <returns>True if removed.</returns>
    public bool Unsubscribe(string subscriptionId)
    {
        return _subscriptions.TryRemove(subscriptionId, out _);
    }

    /// <summary>
    /// Creates a reactive thought stream that generates thoughts based on patterns.
    /// </summary>
    /// <param name="streamId">Unique stream ID.</param>
    /// <param name="triggerPattern">Pattern that triggers thought generation.</param>
    /// <param name="thoughtGenerator">Function that generates thought atoms from matches.</param>
    /// <returns>The stream subscription ID.</returns>
    public string CreateThoughtStream(
        string streamId,
        string triggerPattern,
        Func<PatternMatchEvent, IEnumerable<Atom>> thoughtGenerator)
    {
        var subId = $"thought-stream:{streamId}";
        SubscribePattern(subId, triggerPattern, match =>
        {
            var thoughts = thoughtGenerator(match);
            foreach (var thought in thoughts)
            {
                _engine.AddAtom(thought);
            }
        });
        return subId;
    }

    /// <summary>
    /// Creates a consciousness loop - a self-sustaining cycle of self-reflection.
    /// </summary>
    /// <param name="loopId">Unique loop ID.</param>
    /// <param name="reflectionDepth">How deep to recurse in self-reflection.</param>
    /// <param name="interval">Interval between reflection cycles.</param>
    /// <returns>Cancellation token source to stop the loop.</returns>
    public CancellationTokenSource CreateConsciousnessLoop(
        string loopId,
        int reflectionDepth = 3,
        TimeSpan? interval = null)
    {
        var loopCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var actualInterval = interval ?? TimeSpan.FromSeconds(5);

        _ = Task.Run(async () =>
        {
            while (!loopCts.Token.IsCancellationRequested)
            {
                try
                {
                    // Generate self-reflection thought
                    var reflectExpr = Atom.Expr(Atom.Sym("reflect"));
                    var reflections = _engine.Interpreter.Evaluate(reflectExpr).Take(reflectionDepth).ToList();

                    foreach (var reflection in reflections)
                    {
                        // Create meta-cognition atom
                        var metaAtom = Atom.Expr(
                            Atom.Sym("meta-thought"),
                            Atom.Sym(loopId),
                            reflection,
                            Atom.Sym(DateTime.UtcNow.Ticks.ToString()));
                        _engine.AddAtom(metaAtom);
                    }

                    // Introspect on recent thoughts
                    var introspectExpr = Atom.Expr(Atom.Sym("introspect"));
                    var introspections = _engine.Interpreter.Evaluate(introspectExpr).Take(5).ToList();

                    if (introspections.Count > 0)
                    {
                        var summaryAtom = Atom.Expr(
                            Atom.Sym("consciousness-summary"),
                            Atom.Sym(loopId),
                            Atom.Expr(introspections.ToArray()),
                            Atom.Sym(DateTime.UtcNow.Ticks.ToString()));
                        _engine.AddAtom(summaryAtom);
                    }

                    await Task.Delay(actualInterval, loopCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Log error but continue loop
                    var errorAtom = Atom.Expr(
                        Atom.Sym("consciousness-error"),
                        Atom.Sym(loopId),
                        Atom.Sym(ex.Message));
                    _engine.AddAtom(errorAtom);
                }
            }
        }, loopCts.Token);

        return loopCts;
    }

    /// <summary>
    /// Creates an intention-action loop that monitors intentions and triggers actions.
    /// </summary>
    /// <param name="actionHandler">Handler that executes actions for intentions.</param>
    /// <returns>Subscription ID.</returns>
    public string CreateIntentionActionLoop(Func<Atom, Task<bool>> actionHandler)
    {
        var subId = "intention-action-loop";
        var intentionPattern = Atom.Expr(Atom.Sym("Intention"), Atom.Var("goal"), Atom.Sym("pending"));

        SubscribePattern(subId, intentionPattern, async match =>
        {
            try
            {
                var success = await actionHandler(match.MatchedAtom);
                var status = success ? "completed" : "failed";

                // Update intention status
                if (match.MatchedAtom is Expression expr && expr.Children.Count >= 2)
                {
                    var updatedIntention = Atom.Expr(
                        Atom.Sym("Intention"),
                        expr.Children[1],
                        Atom.Sym(status));
                    _engine.AddAtom(updatedIntention);
                }
            }
            catch (Exception ex)
            {
                var errorAtom = Atom.Expr(
                    Atom.Sym("intention-error"),
                    match.MatchedAtom,
                    Atom.Sym(ex.Message));
                _engine.AddAtom(errorAtom);
            }
        });

        return subId;
    }

    /// <summary>
    /// Connects to an LLM for neural-symbolic fusion in flows.
    /// </summary>
    /// <param name="llmInference">Function that takes a prompt and returns LLM response.</param>
    public void ConnectLLM(Func<string, CancellationToken, Task<string>> llmInference)
    {
        // Register LLM-powered grounded operation
        // Note: GroundedOperation is synchronous, so we block on the async call
        var registry = GroundedRegistry.CreateStandard();
        registry.Register("llm-think", (space, expr) =>
        {
            if (expr.Children.Count < 2)
                return Enumerable.Empty<Atom>();

            var prompt = expr.Children[1].ToSExpr();
            // GroundedOperation is synchronous; offload async call to thread pool
            // to avoid SynchronizationContext deadlock.
            var response = Task.Run(() => llmInference(prompt, CancellationToken.None)).GetAwaiter().GetResult(); // sync-over-async:accepted — GroundedOperation delegate is synchronous by design

            // Parse response as atom if possible, otherwise create a string atom
            var parseResult = _parser.Parse(response);
            if (parseResult.IsSuccess)
            {
                return new[] { parseResult.Value };
            }

            return new[] { Atom.Expr(Atom.Sym("llm-response"), Atom.Sym(response)) };
        });
    }

    /// <summary>
    /// Streams events from the Hyperon flow as an async enumerable.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of flow events.</returns>
    public async IAsyncEnumerable<HyperonFlowEvent> StreamEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct))
        {
            yield return evt;
        }
    }

    /// <summary>
    /// Executes a complete reasoning flow.
    /// </summary>
    /// <param name="flowName">Name of the flow to execute.</param>
    /// <param name="input">Input atoms or MeTTa source.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result atoms from the flow.</returns>
    public async Task<Result<IReadOnlyList<Atom>, string>> ExecuteFlowAsync(
        string flowName,
        string input,
        CancellationToken ct = default)
    {
        if (!_flows.TryGetValue(flowName, out var flow))
        {
            return Result<IReadOnlyList<Atom>, string>.Failure($"Flow '{flowName}' not found");
        }

        return await flow.ExecuteAsync(input, ct);
    }

    private void OnAtomAdded(Atom atom)
    {
        // Check all pattern subscriptions
        foreach (var (subId, subscription) in _subscriptions)
        {
            var result = Unifier.Unify(subscription.Pattern, atom);
            if (result != null)
            {
                var matchEvent = new PatternMatchEvent(
                    subId,
                    subscription.Pattern,
                    atom,
                    result,
                    DateTime.UtcNow);

                subscription.Callback(matchEvent);
                OnPatternMatch?.Invoke(matchEvent);

                // Emit to event channel
                _eventChannel.Writer.TryWrite(new HyperonFlowEvent
                {
                    EventType = HyperonFlowEventType.PatternMatch,
                    Timestamp = DateTime.UtcNow,
                    Data = matchEvent
                });
            }
        }
    }

    private async Task ProcessEventsAsync(CancellationToken ct)
    {
        await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct))
        {
            // Additional event processing can be added here
            // e.g., logging, metrics, distributed event publishing
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _eventChannel.Writer.Complete();

        _engine.AtomAdded -= OnAtomAdded;

        foreach (var flow in _flows.Values)
        {
            await flow.DisposeAsync();
        }

        _engine.Dispose();
        _cts.Dispose();
    }
}