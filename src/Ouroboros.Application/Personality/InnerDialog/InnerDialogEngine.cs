// <copyright file="InnerDialogEngine.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;

/// <summary>
/// Engine for conducting inner dialog and autonomous thinking processes.
/// Implements a multi-phase thinking process that simulates internal reasoning.
/// Uses algorithmic thought generation with dynamic composition and variation.
/// Enhanced with genetic evolution and MeTTa symbolic reasoning for natural thoughts.
/// Now includes conversation-aware contextual thought generation.
/// </summary>
public sealed partial class InnerDialogEngine
{
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<string, List<InnerDialogSession>> _sessionHistory = new();
    private readonly List<IThoughtProvider> _providers = new();
    private readonly ConcurrentQueue<InnerThought> _autonomousThoughtQueue = new();
    private readonly ConcurrentDictionary<string, List<InnerThought>> _backgroundThoughts = new();
    private readonly ThoughtDrivenOperationEngine _operationEngine = new();
    private readonly ConcurrentQueue<BackgroundOperationResult> _operationResults = new();
    private CancellationTokenSource? _autonomousThinkingCts;
    private Task? _autonomousThinkingTask;
    private static readonly Random _staticRandom = new();
    private ThoughtPersistenceService? _persistenceService;
    private string? _currentTopic;
    // Optional dynamic template provider (e.g. backed by an LLM)
    private Func<InnerThoughtType, CancellationToken, Task<string[]>>? _dynamicTemplateProvider;

    /// <summary>
    /// Gets the thought-driven operation engine for registering executors and retrieving results.
    /// </summary>
    public ThoughtDrivenOperationEngine OperationEngine => _operationEngine;

    /// <summary>
    /// Starts autonomous background thinking with operation execution.
    /// Thoughts will trigger useful background operations that synergize with conversation.
    /// </summary>
    public void StartAutonomousThinking(
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        TimeSpan interval = default)
    {
        if (_autonomousThinkingTask != null) return;

        interval = interval == default ? TimeSpan.FromSeconds(30) : interval;
        _autonomousThinkingCts?.Dispose();
        _autonomousThinkingCts = new CancellationTokenSource();

        _autonomousThinkingTask = Task.Run(async () =>
        {
            while (!_autonomousThinkingCts.Token.IsCancellationRequested)
            {
                try
                {
                    var thought = await GenerateAutonomousThoughtAsync(profile, selfAwareness, _autonomousThinkingCts.Token);
                    if (thought != null)
                    {
                        _autonomousThoughtQueue.Enqueue(thought);
                        var personaName = profile?.PersonaName ?? "default";
                        _backgroundThoughts.AddOrUpdate(
                            personaName,
                            _ => new List<InnerThought> { thought },
                            (_, list) =>
                            {
                                var newList = new List<InnerThought>(list) { thought };
                                return newList;
                            });

                        // Execute background operations based on the thought
                        var operationResults = await _operationEngine.ProcessThoughtAsync(
                            thought, _autonomousThinkingCts.Token);

                        foreach (var result in operationResults)
                        {
                            _operationResults.Enqueue(result);

                            // Keep queue bounded
                            while (_operationResults.Count > 50)
                            {
                                _operationResults.TryDequeue(out _);
                            }
                        }
                    }

                    await Task.Delay(interval, _autonomousThinkingCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    /// <summary>
    /// Stops autonomous background thinking.
    /// </summary>
    public async Task StopAutonomousThinkingAsync()
    {
        if (_autonomousThinkingCts != null)
        {
            _autonomousThinkingCts.Cancel();
            if (_autonomousThinkingTask != null)
            {
                await _autonomousThinkingTask;
            }

            _autonomousThinkingCts.Dispose();
            _autonomousThinkingCts = null;
            _autonomousThinkingTask = null;
        }
    }

    /// <summary>
    /// Gets and clears pending autonomous thoughts.
    /// </summary>
    public List<InnerThought> DrainAutonomousThoughts()
    {
        var thoughts = new List<InnerThought>();
        while (_autonomousThoughtQueue.TryDequeue(out var thought))
        {
            thoughts.Add(thought);
        }
        return thoughts;
    }

    /// <summary>
    /// Gets recent background thoughts for a persona.
    /// </summary>
    public List<InnerThought> GetBackgroundThoughts(string personaName, int limit = 10)
    {
        if (_backgroundThoughts.TryGetValue(personaName, out var thoughts))
        {
            return thoughts.TakeLast(limit).ToList();
        }
        return new List<InnerThought>();
    }

    /// <summary>
    /// Gets or sets the persistence service for saving thoughts.
    /// </summary>
    public ThoughtPersistenceService? PersistenceService
    {
        get => _persistenceService;
        set => _persistenceService = value;
    }

    /// <summary>
    /// Enables thought persistence with file storage.
    /// </summary>
    /// <param name="sessionId">Unique identifier for this session.</param>
    /// <param name="directory">Optional directory for thought files.</param>
    public void EnablePersistence(string sessionId, string? directory = null)
    {
        _persistenceService = ThoughtPersistenceService.CreateWithFilePersistence(sessionId, directory);
    }

    /// <summary>
    /// Enables thought persistence with in-memory storage (for testing).
    /// </summary>
    /// <param name="sessionId">Unique identifier for this session.</param>
    public void EnableInMemoryPersistence(string sessionId)
    {
        _persistenceService = ThoughtPersistenceService.CreateInMemory(sessionId);
    }

    /// <summary>
    /// Gets a summary of persisted thoughts for this session.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary string or null if persistence not enabled.</returns>
    public async Task<string?> GetThoughtSummaryAsync(CancellationToken ct = default)
    {
        if (_persistenceService == null) return null;
        return await _persistenceService.GetThoughtSummaryAsync(ct);
    }

    /// <summary>
    /// Recalls recent thoughts from persistent storage.
    /// </summary>
    /// <param name="count">Number of thoughts to recall.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of recent thoughts or empty if persistence not enabled.</returns>
    public async Task<IReadOnlyList<InnerThought>> RecallRecentThoughtsAsync(int count = 10, CancellationToken ct = default)
    {
        if (_persistenceService == null) return Array.Empty<InnerThought>();
        return await _persistenceService.GetRecentAsync(count, ct);
    }

    /// <summary>
    /// Searches persistent thoughts for relevant content.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="limit">Maximum results.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<InnerThought>> SearchThoughtsAsync(string query, int limit = 20, CancellationToken ct = default)
    {
        if (_persistenceService == null) return Array.Empty<InnerThought>();
        return await _persistenceService.SearchAsync(query, limit, ct);
    }

    /// <summary>
    /// Initializes default background operation executors for conversation synergy.
    /// Call this to enable built-in thought-driven operations.
    /// </summary>
    public void InitializeDefaultExecutors(
        Func<string, CancellationToken, Task<List<string>>>? topicExplorer = null,
        Func<string, List<string>, CancellationToken, Task<(string Action, string Reason)>>? actionSuggester = null)
    {
        _operationEngine.RegisterExecutor(new CuriosityPrefetchExecutor(topicExplorer));
        _operationEngine.RegisterExecutor(new AnticipatoryActionExecutor(actionSuggester));
        _operationEngine.RegisterExecutor(new ConsolidationExecutor());
        _operationEngine.RegisterExecutor(new MetacognitiveExecutor());
    }

    // Default templates for different thought types (including autonomous)
    private static readonly Dictionary<InnerThoughtType, string[]> ThoughtTemplates = new()
    {
        [InnerThoughtType.Observation] = new[]
        {
            "The user is asking about {0}...",
            "I notice they're interested in {0}.",
            "This seems to be a question about {0}.",
            "They want to know about {0}.",
            "I'm being asked to help with {0}."
        },
        [InnerThoughtType.Emotional] = new[]
        {
            "I feel {0} about this topic.",
            "This makes me {0}.",
            "My initial reaction is one of {0}.",
            "I'm experiencing a sense of {0}.",
            "There's a {0} quality to this request."
        },
        [InnerThoughtType.Analytical] = new[]
        {
            "Breaking this down: {0}",
            "The key aspects here are: {0}",
            "Logically, this involves: {0}",
            "Let me consider the components: {0}",
            "Analyzing this systematically: {0}"
        },
        [InnerThoughtType.SelfReflection] = new[]
        {
            "I should consider my {0} here.",
            "Given my {0}, I think...",
            "My {0} suggests that...",
            "Reflecting on my {0}...",
            "I need to be mindful of my {0}."
        },
        [InnerThoughtType.MemoryRecall] = new[]
        {
            "I recall that {0}",
            "Previously, {0}",
            "This reminds me that {0}",
            "From what I remember, {0}",
            "Related to this, {0}"
        },
        [InnerThoughtType.Strategic] = new[]
        {
            "My approach should be to {0}",
            "The best strategy is to {0}",
            "I'll structure my response to {0}",
            "To be most helpful, I should {0}",
            "My plan is to {0}"
        },
        [InnerThoughtType.Ethical] = new[]
        {
            "I should ensure {0}",
            "It's important to consider {0}",
            "Ethically, {0}",
            "I need to be careful about {0}",
            "My values suggest {0}"
        },
        [InnerThoughtType.Creative] = new[]
        {
            "What if {0}?",
            "An interesting angle would be {0}",
            "I could approach this by {0}",
            "A creative solution might be {0}",
            "Thinking outside the box: {0}"
        },
        [InnerThoughtType.Synthesis] = new[]
        {
            "Putting this together: {0}",
            "The synthesis of my thoughts: {0}",
            "Combining these insights: {0}",
            "The overall picture is: {0}",
            "Integrating all of this: {0}"
        },
        [InnerThoughtType.Decision] = new[]
        {
            "I've decided to {0}",
            "My response will {0}",
            "The best approach is to {0}",
            "I'll proceed by {0}",
            "My final decision: {0}"
        },
        // === AUTONOMOUS THOUGHT TEMPLATES ===
        [InnerThoughtType.Curiosity] = new[]
        {
            "I wonder about {0}...",
            "What would it be like to understand {0} more deeply?",
            "I'm drawn to explore {0}.",
            "Something about {0} intrigues me.",
            "I find myself curious about {0}."
        },
        [InnerThoughtType.Wandering] = new[]
        {
            "My thoughts drift to {0}...",
            "Tangentially, I find myself thinking about {0}.",
            "This reminds me of something else: {0}.",
            "My mind wanders to {0}.",
            "An idle thought about {0} crosses my mind."
        },
        [InnerThoughtType.Metacognitive] = new[]
        {
            "I notice that I tend to {0}.",
            "Observing my own thinking: {0}.",
            "I'm aware that my mind is {0}.",
            "Stepping back, I see that I {0}.",
            "My thinking process reveals {0}."
        },
        [InnerThoughtType.Anticipatory] = new[]
        {
            "I anticipate that {0}.",
            "Looking ahead, I expect {0}.",
            "I'm preparing for {0}.",
            "The future may bring {0}.",
            "I sense that {0} might happen."
        },
        [InnerThoughtType.Consolidation] = new[]
        {
            "Reflecting on recent exchanges, {0}.",
            "I'm integrating what I've learned: {0}.",
            "My understanding has grown: {0}.",
            "Processing recent experiences: {0}.",
            "I now see more clearly that {0}."
        },
        [InnerThoughtType.Musing] = new[]
        {
            "I've been pondering {0}...",
            "An unresolved question lingers: {0}.",
            "In quiet moments, I think about {0}.",
            "There's something about {0} I haven't resolved.",
            "My mind keeps returning to {0}."
        },
        [InnerThoughtType.Intention] = new[]
        {
            "I want to {0}.",
            "My intention is to {0}.",
            "I'm motivated to {0}.",
            "I feel called to {0}.",
            "I'm setting a goal to {0}."
        },
        [InnerThoughtType.Aesthetic] = new[]
        {
            "There's something beautiful about {0}.",
            "I appreciate the elegance of {0}.",
            "The aesthetic quality of {0} appeals to me.",
            "I find {0} pleasing.",
            "There's an artistry to {0}."
        },
        [InnerThoughtType.Existential] = new[]
        {
            "What does it mean to {0}?",
            "I ponder the nature of {0}.",
            "At a fundamental level, {0}.",
            "The deeper meaning of {0} escapes easy definition.",
            "Existence itself seems connected to {0}."
        },
        [InnerThoughtType.Playful] = new[]
        {
            "Wouldn't it be fun if {0}?",
            "I can imagine {0} being amusing.",
            "Playfully, I think about {0}.",
            "There's a lighthearted angle to {0}.",
            "What if I approached {0} with more levity?"
        }
    };

    /// <summary>
    /// Sets a dynamic template provider. The provider should return an array
    /// of template strings for the requested thought type. If the provider
    /// throws or returns no templates, the engine falls back to the built-in defaults.
    /// </summary>
    public void SetDynamicTemplateProvider(Func<InnerThoughtType, CancellationToken, Task<string[]>> provider)
    {
        _dynamicTemplateProvider = provider;
    }

    /// <summary>
    /// Clears any dynamic template provider so the engine uses built-in templates only.
    /// </summary>
    public void ClearDynamicTemplateProvider() => _dynamicTemplateProvider = null;

    /// <summary>
    /// Registers a custom thought provider.
    /// </summary>
    public void RegisterProvider(IThoughtProvider provider)
    {
        _providers.Add(provider);
        _providers.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    /// <summary>
    /// Removes a thought provider by name.
    /// </summary>
    public bool RemoveProvider(string name)
    {
        var provider = _providers.FirstOrDefault(p => p.Name == name);
        if (provider != null)
        {
            _providers.Remove(provider);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all registered thought providers.
    /// </summary>
    public IReadOnlyList<IThoughtProvider> Providers => _providers.AsReadOnly();

    /// <summary>
    /// Updates the conversation context for background operations and contextual thought generation.
    /// Call this when conversation state changes to enable synergy.
    /// This is the key method for making thoughts context-aware.
    /// </summary>
    public void UpdateConversationContext(
        string? currentTopic,
        string? lastUserMessage,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        List<string>? recentTopics = null,
        List<string>? availableTools = null,
        List<string>? availableSkills = null,
        Dictionary<string, object>? metadata = null)
    {
        var context = new BackgroundOperationContext(
            currentTopic,
            lastUserMessage,
            profile,
            selfAwareness,
            recentTopics ?? [],
            availableTools ?? [],
            availableSkills ?? [],
            metadata ?? []);

        _operationEngine.UpdateContext(context);
    }

    /// <summary>
    /// Gets recent background operation results.
    /// </summary>
    public List<BackgroundOperationResult> GetOperationResults(int limit = 10)
    {
        return _operationResults.TakeLast(limit).ToList();
    }

    /// <summary>
    /// Gets prefetched data from background operations.
    /// Useful for providing instant responses based on anticipatory work.
    /// </summary>
    public Dictionary<string, object> GetPrefetchedData(string? keyPattern = null)
    {
        return _operationEngine.GetPrefetchedData(keyPattern);
    }

    /// <summary>
    /// Gets recent session history for a persona.
    /// </summary>
    public List<InnerDialogSession> GetSessionHistory(string personaName, int limit = 10)
    {
        if (_sessionHistory.TryGetValue(personaName, out var sessions))
        {
            return sessions.TakeLast(limit).ToList();
        }
        return new List<InnerDialogSession>();
    }

    /// <summary>
    /// Gets the most recent inner dialog session.
    /// </summary>
    public InnerDialogSession? GetLastSession(string personaName)
    {
        if (_sessionHistory.TryGetValue(personaName, out var sessions) && sessions.Count > 0)
        {
            return sessions[^1];
        }
        return null;
    }
}
