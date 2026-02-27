// <copyright file="AutonomousMind.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Collections.Concurrent;
using System.Text;
using Ouroboros.Application.Personality;

/// <summary>
/// Autonomous mind that thinks, explores, and acts independently in the background.
/// Enables curiosity-driven learning, proactive actions, and self-directed exploration.
/// Uses pipeline monads for structured reasoning and persists state continuously.
/// Integrates with InnerDialogEngine for algorithmic thought generation.
/// </summary>
public partial class AutonomousMind : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentQueue<Thought> _thoughtStream = new();
    private readonly ConcurrentQueue<string> _curiosityQueue = new();
    private readonly ConcurrentQueue<AutonomousAction> _pendingActions = new();
    private readonly List<string> _learnedFacts = [];
    private readonly List<string> _interests = [];
    private readonly HashSet<string> _recentTopicKeywords = []; // Tracks recent topics to avoid repetition
    private int _topicRotationCounter; // Forces topic diversity

    // Anti-hallucination tracking — delegated to ClaimVerificationService
    private readonly IClaimVerificationService _claimVerification;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutonomousMind"/> class
    /// with a default <see cref="ClaimVerificationService"/>.
    /// </summary>
    public AutonomousMind()
        : this(new ClaimVerificationService())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AutonomousMind"/> class
    /// with the specified claim verification service.
    /// </summary>
    /// <param name="claimVerification">The claim verification service to use.</param>
    public AutonomousMind(IClaimVerificationService claimVerification)
    {
        _claimVerification = claimVerification ?? throw new ArgumentNullException(nameof(claimVerification));
    }

    private Task? _thinkingTask;
    private Task? _curiosityTask;
    private Task? _actionTask;
    private Task? _persistenceTask;
    private bool _isActive;
    private DateTime _lastThought = DateTime.MinValue;
    private int _thoughtCount;

    /// <summary>
    /// When true, suppresses proactive messages (e.g., during problem-solving mode).
    /// </summary>
    public bool SuppressProactiveMessages { get; set; }

    // Emotional state tracking
    private EmotionalState _currentEmotion = new();
    private readonly ConcurrentQueue<EmotionalState> _emotionalHistory = new();

    // Integration with InnerDialogEngine for sophisticated thought generation
    private InnerDialogEngine? _innerDialog;
    private PersonalityProfile? _personalityProfile;
    private SelfAwareness? _selfAwareness;

    /// <summary>
    /// Delegate for generating AI responses (used for deep exploration, not routine thoughts).
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? ThinkFunction { get; set; }

    /// <summary>
    /// Delegate for pipeline-based reasoning (uses monadic composition).
    /// </summary>
    public Func<string, PipelineBranch?, CancellationToken, Task<(string Result, PipelineBranch Branch)>>? PipelineThinkFunction { get; set; }

    /// <summary>
    /// Delegate for persisting network state and learnings.
    /// </summary>
    public Func<string, string, double, CancellationToken, Task>? PersistLearningFunction { get; set; }

    /// <summary>
    /// Delegate for persisting emotional state.
    /// </summary>
    public Func<EmotionalState, CancellationToken, Task>? PersistEmotionFunction { get; set; }

    /// <summary>
    /// Delegate for web search.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? SearchFunction { get; set; }

    /// <summary>
    /// Delegate for executing tools.
    /// Also forwarded to the <see cref="IClaimVerificationService"/> when backed by <see cref="ClaimVerificationService"/>.
    /// </summary>
    public Func<string, string, CancellationToken, Task<string>>? ExecuteToolFunction
    {
        get => _executeToolFunction;
        set
        {
            _executeToolFunction = value;
            if (_claimVerification is ClaimVerificationService svc)
                svc.ExecuteToolFunction = value;
        }
    }
    private Func<string, string, CancellationToken, Task<string>>? _executeToolFunction;

    /// <summary>
    /// Delegate for executing piped commands (supports internal | piping).
    /// Takes a command string that may contain pipe operators.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? ExecutePipeCommandFunction { get; set; }

    /// <summary>
    /// Delegate for sanitizing raw tool outputs into natural conversational text.
    /// Takes raw output string, returns sanitized natural language.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? SanitizeOutputFunction { get; set; }

    /// <summary>
    /// Self-indexer for knowledge reorganization during thinking.
    /// When set, enables learning-driven knowledge consolidation.
    /// </summary>
    public QdrantSelfIndexer? SelfIndexer { get; set; }

    /// <summary>
    /// Delegate for verifying file existence. Returns true if file exists.
    /// Used to prevent hallucination about non-existent files.
    /// Also forwarded to the <see cref="IClaimVerificationService"/> when backed by <see cref="ClaimVerificationService"/>.
    /// </summary>
    public Func<string, bool>? VerifyFileExistsFunction
    {
        get => _verifyFileExistsFunction;
        set
        {
            _verifyFileExistsFunction = value;
            if (_claimVerification is ClaimVerificationService svc)
                svc.VerifyFileExistsFunction = value;
        }
    }
    private Func<string, bool>? _verifyFileExistsFunction;

    /// <summary>
    /// Delegate for computing file hash. Returns hash string or null if file doesn't exist.
    /// Used to verify modifications actually occurred.
    /// Also forwarded to the <see cref="IClaimVerificationService"/> when backed by <see cref="ClaimVerificationService"/>.
    /// </summary>
    public Func<string, string?>? ComputeFileHashFunction
    {
        get => _computeFileHashFunction;
        set
        {
            _computeFileHashFunction = value;
            if (_claimVerification is ClaimVerificationService svc)
                svc.ComputeFileHashFunction = value;
        }
    }
    private Func<string, string?>? _computeFileHashFunction;

    // Track reorganization cycles
    private int _reorganizationCycle;
    private DateTime _lastReorganization = DateTime.MinValue;

    /// <summary>
    /// Event fired when a new thought is generated.
    /// </summary>
    public event Action<Thought>? OnThought;

    /// <summary>
    /// Event fired when curiosity leads to a discovery.
    /// </summary>
    public event Action<string, string>? OnDiscovery;

    /// <summary>
    /// Event fired when an autonomous action is taken.
    /// </summary>
    public event Action<AutonomousAction>? OnAction;

    /// <summary>
    /// Event fired when the mind wants to say something proactively.
    /// </summary>
    public event Action<string>? OnProactiveMessage;

    /// <summary>
    /// Event fired when emotional state changes.
    /// </summary>
    public event Action<EmotionalState>? OnEmotionalChange;

    /// <summary>
    /// Event fired when state is persisted.
    /// </summary>
    public event Action<string>? OnStatePersisted;

    /// <summary>
    /// Gets current thinking state.
    /// </summary>
    public bool IsThinking => _isActive;

    /// <summary>
    /// Gets thought count.
    /// </summary>
    public int ThoughtCount => _thoughtCount;

    /// <summary>
    /// Gets recent thoughts.
    /// </summary>
    public IEnumerable<Thought> RecentThoughts => _thoughtStream.TakeLast(20);

    /// <summary>
    /// Gets learned facts.
    /// </summary>
    public IReadOnlyList<string> LearnedFacts => _learnedFacts.AsReadOnly();

    /// <summary>
    /// Gets current emotional state.
    /// </summary>
    public EmotionalState CurrentEmotion => _currentEmotion;

    /// <summary>
    /// Gets the current pipeline branch for reasoning (if pipeline is in use).
    /// </summary>
    public PipelineBranch? CurrentBranch { get; private set; }

    /// <summary>
    /// Configuration for autonomous behavior.
    /// </summary>
    public AutonomousConfig Config { get; set; } = new();

    /// <summary>
    /// Gets or sets the culture for localized messages (e.g., "de-DE" for German).
    /// </summary>
    public string? Culture { get; set; }

    /// <summary>
    /// Update the emotional state during thinking.
    /// </summary>
    public void UpdateEmotion(double arousal, double valence, string dominantEmotion)
    {
        var newEmotion = new EmotionalState
        {
            Arousal = Math.Clamp(arousal, -1.0, 1.0),
            Valence = Math.Clamp(valence, -1.0, 1.0),
            DominantEmotion = dominantEmotion,
            Timestamp = DateTime.UtcNow,
        };

        _currentEmotion = newEmotion;
        _emotionalHistory.Enqueue(newEmotion);

        // Keep emotional history manageable
        while (_emotionalHistory.Count > 50)
        {
            _emotionalHistory.TryDequeue(out _);
        }

        OnEmotionalChange?.Invoke(newEmotion);
    }

    /// <summary>
    /// Inject a topic for the mind to think about.
    /// </summary>
    public void InjectTopic(string topic)
    {
        _curiosityQueue.Enqueue(topic);
    }

    /// <summary>
    /// Add an interest for curiosity-driven exploration.
    /// </summary>
    public void AddInterest(string interest)
    {
        if (!_interests.Contains(interest, StringComparer.OrdinalIgnoreCase))
        {
            _interests.Add(interest);
        }
    }

    /// <summary>
    /// Get a summary of the autonomous mind's state.
    /// </summary>
    public string GetMindState()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\ud83e\udde0 **Autonomous Mind State**\n");
        sb.AppendLine($"**Status:** {(_isActive ? "Active \ud83d\udfe2" : "Dormant \ud83d\udd34")}");
        sb.AppendLine($"**Thoughts Generated:** {_thoughtCount}");
        sb.AppendLine($"**Facts Learned:** {_learnedFacts.Count}");
        sb.AppendLine($"**Active Interests:** {_interests.Count}");
        sb.AppendLine($"**Pending Curiosities:** {_curiosityQueue.Count}");
        sb.AppendLine($"**Pending Actions:** {_pendingActions.Count}");

        if (_interests.Count > 0)
        {
            sb.AppendLine($"\n**Interests:** {string.Join(", ", _interests.Take(10))}");
        }

        if (_learnedFacts.Count > 0)
        {
            sb.AppendLine("\n**Recent Discoveries:**");
            foreach (var fact in _learnedFacts.TakeLast(5))
            {
                sb.AppendLine($"  \u2022 {fact}");
            }
        }

        var recentThoughts = _thoughtStream.TakeLast(3).ToList();
        if (recentThoughts.Count > 0)
        {
            sb.AppendLine("\n**Recent Thoughts:**");
            foreach (var thought in recentThoughts)
            {
                sb.AppendLine($"  \ud83d\udcad [{thought.Type}] {thought.Content.Substring(0, Math.Min(100, thought.Content.Length))}...");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Start autonomous thinking and exploration.
        /// </summary>
    public void Start()
    {
        if (_isActive) return;
        _isActive = true;

        _thinkingTask = Task.Run(ThinkingLoopAsync);
        _curiosityTask = Task.Run(CuriosityLoopAsync);
        _actionTask = Task.Run(ActionLoopAsync);
        _persistenceTask = Task.Run(PersistenceLoopAsync);

        OnProactiveMessage?.Invoke(Localize("\ud83e\udde0 My autonomous mind is now active. I'll think, explore, and learn in the background."));
    }

    /// <summary>
    /// Stop autonomous thinking.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isActive) return;
        _isActive = false;
        _cts.Cancel();

        if (_thinkingTask != null) await _thinkingTask.ConfigureAwait(false);
        if (_curiosityTask != null) await _curiosityTask.ConfigureAwait(false);
        if (_actionTask != null) await _actionTask.ConfigureAwait(false);
        if (_persistenceTask != null) await _persistenceTask.ConfigureAwait(false);

        // Final state persistence
        await PersistCurrentStateAsync("shutdown");

        OnProactiveMessage?.Invoke(Localize("\ud83d\udca4 Autonomous mind entering rest state. State persisted."));
    }

    /// <summary>
    /// Get anti-hallucination statistics for monitoring.
    /// Delegates to <see cref="IClaimVerificationService"/>.
    /// </summary>
    public AntiHallucinationStats GetAntiHallucinationStats() => _claimVerification.GetAntiHallucinationStats();

    public void Dispose()
    {
        _isActive = false;
        _cts.Cancel();
        _cts.Dispose();
    }
}
