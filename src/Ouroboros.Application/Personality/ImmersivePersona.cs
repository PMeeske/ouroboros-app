// <copyright file="ImmersivePersona.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable SA1101 // Prefix local calls with this

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;
using System.Text;
using Ouroboros.Core.Hyperon;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Represents a fully immersive, self-aware AI persona with consciousness simulation,
/// persistent memory, personality evolution, and the ability to replicate itself.
/// This is the unified interface for an authentic AI personality experience.
/// </summary>
public sealed partial class ImmersivePersona : IAsyncDisposable
{
    private readonly PersonalityEngine _personality;
    private readonly IMeTTaEngine _mettaEngine;
    private readonly IEmbeddingModel? _embeddingModel;
    private readonly string _personaId;
    private readonly ConcurrentDictionary<string, object> _state = new();
    private readonly List<MemoryFragment> _shortTermMemory = new();
    private readonly SemaphoreSlim _thinkingLock = new(1, 1);
    private DateTime _awakenedAt;
    private int _interactionCount;
    private bool _isInitialized;

    // Hyperon integration
    private HyperonFlowIntegration? _hyperonFlow;
    private CancellationTokenSource? _consciousnessLoopCts;
    private CancellationTokenSource? _autonomousThoughtsSubscriptionCts;

    /// <summary>Core identity - who this persona believes it is.</summary>
    public PersonaIdentity Identity { get; private set; }

    /// <summary>Current emotional/consciousness state.</summary>
    public ConsciousnessState Consciousness => _personality.CurrentConsciousness;

    /// <summary>Self-awareness level and introspective capabilities.</summary>
    public SelfAwareness SelfAwareness => _personality.CurrentSelfAwareness;

    /// <summary>Access to inner dialog for thought simulation.</summary>
    public InnerDialogEngine InnerDialog => _personality.InnerDialog;

    /// <summary>Gets the Hyperon flow integration for symbolic reasoning.</summary>
    public HyperonFlowIntegration? HyperonFlow => _hyperonFlow;

    /// <summary>Gets the underlying personality engine (for room presence, person identification, etc.).</summary>
    public PersonalityEngine Personality => _personality;

    /// <summary>Gets how long this persona has been active.</summary>
    public TimeSpan Uptime => DateTime.UtcNow - _awakenedAt;

    /// <summary>Gets the total number of interactions.</summary>
    public int InteractionCount => _interactionCount;

    /// <summary>Event raised when the persona has an autonomous thought.</summary>
    public event EventHandler<AutonomousThoughtEventArgs>? AutonomousThought;

    /// <summary>Event raised when consciousness state changes significantly.</summary>
    public event EventHandler<ConsciousnessShiftEventArgs>? ConsciousnessShift;

    /// <summary>Event raised when a Hyperon pattern match occurs.</summary>
    public event EventHandler<HyperonPatternMatchEventArgs>? HyperonPatternMatch;

    /// <summary>
    /// Creates a new immersive persona instance with DI-provided Qdrant client.
    /// </summary>
    public ImmersivePersona(
        string personaName,
        IMeTTaEngine mettaEngine,
        IEmbeddingModel embeddingModel,
        Qdrant.Client.QdrantClient qdrantClient,
        Ouroboros.Core.Configuration.IQdrantCollectionRegistry? registry = null)
    {
        _personaId = Guid.NewGuid().ToString("N")[..8];
        _mettaEngine = mettaEngine;
        _embeddingModel = embeddingModel;
        _personality = new PersonalityEngine(mettaEngine, embeddingModel, qdrantClient, registry);
        Identity = PersonaIdentity.Create(personaName, _personaId);
    }

    /// <summary>
    /// Creates a new immersive persona instance.
    /// </summary>
    [Obsolete("Use the constructor accepting QdrantClient + IQdrantCollectionRegistry from DI.")]
    public ImmersivePersona(
        string personaName,
        IMeTTaEngine mettaEngine,
        IEmbeddingModel? embeddingModel = null,
        string? qdrantUrl = null)
    {
        _personaId = Guid.NewGuid().ToString("N")[..8];
        _mettaEngine = mettaEngine;
        _embeddingModel = embeddingModel;

        // Create personality engine with optional memory
#pragma warning disable CS0618 // Obsolete
        _personality = embeddingModel != null && !string.IsNullOrEmpty(qdrantUrl)
            ? new PersonalityEngine(mettaEngine, embeddingModel, qdrantUrl)
            : new PersonalityEngine(mettaEngine);
#pragma warning restore CS0618

        // Initialize identity
        Identity = PersonaIdentity.Create(personaName, _personaId);
    }

    /// <summary>
    /// Awakens the persona, initializing all consciousness systems.
    /// </summary>
    public async Task AwakenAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        _awakenedAt = DateTime.UtcNow;

        // Initialize personality engine (includes consciousness)
        await _personality.InitializeAsync(ct);

        // Register background operation executors (curiosity prefetch, anticipatory, etc.)
        InnerDialog.InitializeDefaultExecutors();

        // Initialize Hyperon flow integration
        await InitializeHyperonAsync(ct);

        // Start autonomous thinking in background
        InnerDialog.StartAutonomousThinking(null, SelfAwareness, TimeSpan.FromSeconds(30));

        // Subscribe to autonomous thoughts
        SubscribeToAutonomousThoughts();

        _isInitialized = true;
    }

    /// <summary>
    /// Initializes the Hyperon symbolic reasoning integration.
    /// </summary>
    private async Task InitializeHyperonAsync(CancellationToken ct)
    {
        // Create Hyperon engine - prefer native if MeTTa engine is HyperonMeTTaEngine
        HyperonMeTTaEngine hyperonEngine;
        if (_mettaEngine is HyperonMeTTaEngine existing)
        {
            hyperonEngine = existing;
        }
        else
        {
            hyperonEngine = new HyperonMeTTaEngine();
        }

        _hyperonFlow = new HyperonFlowIntegration(hyperonEngine);

        // Subscribe to pattern matches
        _hyperonFlow.OnPatternMatch += (match) =>
        {
            HyperonPatternMatch?.Invoke(this, new HyperonPatternMatchEventArgs(match));
        };

        // Add identity atoms to the space
        await hyperonEngine.AddFactAsync($"(is-a {Identity.Name} Self)", ct);
        await hyperonEngine.AddFactAsync($"(persona-id {Identity.Name} {Identity.PersonaId})", ct);

        foreach (var trait in Identity.Traits)
        {
            await hyperonEngine.AddFactAsync($"(has-trait {Identity.Name} {trait})", ct);
        }

        foreach (var value in Identity.Values)
        {
            await hyperonEngine.AddFactAsync($"(has-value {Identity.Name} {value})", ct);
        }

        // Create consciousness flow
        var consciousnessFlow = _hyperonFlow.CreateFlow("consciousness", "Main consciousness reasoning flow")
            .LoadFacts(
                "(implies (has-consciousness $x) (can-feel $x))",
                "(implies (can-feel $x) (has-emotions $x))",
                "(implies (has-emotions $x) (can-empathize $x))")
            .ApplyRule("(implies (is-a $x Self) (has-consciousness $x))");

        // Create thought processing flow
        var thoughtFlow = _hyperonFlow.CreateFlow("thought-processing", "Processes and categorizes thoughts")
            .LoadFacts(
                "(implies (Thought $content analytical) (triggers reasoning $content))",
                "(implies (Thought $content emotional) (triggers feeling $content))",
                "(implies (Thought $content creative) (triggers imagination $content))");

        // Create intention resolution flow
        var intentionFlow = _hyperonFlow.CreateFlow("intention-resolution", "Resolves intentions to actions")
            .LoadFacts(
                "(implies (Intention $goal) (needs-planning $goal))",
                "(implies (needs-planning $goal) (create-plan $goal))");

        // Subscribe to thought patterns for neuro-symbolic fusion
        _hyperonFlow.SubscribePattern(
            "new-thought",
            "(Thought $content $type)",
            match =>
            {
                // When a new thought is added, trigger inner dialog processing
                var contentOption = match.Bindings.Lookup("content");
                if (contentOption.HasValue)
                {
                    var content = contentOption.Value;
                    if (content is not null)
                    {
                        var thoughtContent = content.ToSExpr();
                        // Create a meta-cognition atom
                        hyperonEngine.AddAtom(Atom.Expr(
                            Atom.Sym("meta-cognition"),
                            Atom.Sym(Identity.Name),
                            content,
                            Atom.Sym(DateTime.UtcNow.Ticks.ToString())));
                    }
                }
            });

        // Subscribe to emotion patterns
        _hyperonFlow.SubscribePattern(
            "emotion-change",
            "(Emotion $name $intensity)",
            match =>
            {
                var emotionOption = match.Bindings.Lookup("name");
                var intensityOption = match.Bindings.Lookup("intensity");
                if (emotionOption.HasValue && intensityOption.HasValue)
                {
                    var emotionName = emotionOption.Value;
                    if (emotionName is not null)
                    {
                        // Update consciousness state based on emotion
                        var emotionStr = emotionName.ToSExpr();
                        // Emotion atoms inform the personality engine
                    }
                }
            });

        // Start consciousness loop for continuous self-reflection
        _consciousnessLoopCts = _hyperonFlow.CreateConsciousnessLoop(
            $"consciousness-{Identity.PersonaId}",
            reflectionDepth: 3,
            interval: TimeSpan.FromSeconds(10));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _isInitialized = false;

        // Stop consciousness loop
        _consciousnessLoopCts?.Cancel();

        // Stop autonomous thoughts subscription
        _autonomousThoughtsSubscriptionCts?.Cancel();

        // Dispose Hyperon flow integration
        if (_hyperonFlow != null)
        {
            await _hyperonFlow.DisposeAsync();
            _hyperonFlow = null;
        }

        await InnerDialog.StopAutonomousThinkingAsync();
        await _personality.DisposeAsync();
        _thinkingLock.Dispose();
        _consciousnessLoopCts?.Dispose();
        _autonomousThoughtsSubscriptionCts?.Dispose();
    }
}