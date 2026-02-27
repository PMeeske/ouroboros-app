// <copyright file="PersonalityEngine.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Ouroboros.Domain;
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Ouroboros.Tools.MeTTa;


/// <summary>
/// MeTTa-based personality reasoning engine that uses genetic algorithms
/// to evolve optimal personality expressions and proactive questioning.
/// Integrates with Qdrant for long-term conversation and personality memory.
/// This class acts as a facade delegating to focused sub-engines.
/// </summary>
public sealed class PersonalityEngine : IAsyncDisposable
{
    private readonly IMeTTaEngine _mettaEngine;
    private readonly ConcurrentDictionary<string, PersonalityProfile> _profiles = new();
    private readonly ConcurrentDictionary<string, List<InteractionFeedback>> _feedbackHistory = new();
    private readonly Random _random = new();
    private bool _isInitialized;

    // Qdrant memory integration
    private readonly Qdrant.Client.QdrantClient? _qdrantClient;
    private readonly IEmbeddingModel? _embeddingModel;
    private readonly string ConversationCollectionName = "ouroboros_conversations";
    private readonly string PersonalityCollectionName = "ouroboros_personalities";
    private readonly string PersonCollectionName = "ouroboros_persons";

    // Self-awareness
    private SelfAwareness _selfAwareness = SelfAwareness.Default("Ouroboros");

    // Inner dialog engine
    private readonly InnerDialogEngine _innerDialogEngine = new();

    // Pavlovian consciousness engine
    private readonly PavlovianConsciousnessEngine _consciousness = new();
    private bool _consciousnessInitialized;

    // Sub-engines (delegation targets)
    private readonly MoodEngine _moodEngine;
    private readonly PersonDetectionEngine _personDetectionEngine;
    private readonly PersonalityMemoryManager _memoryManager;
    private readonly RelationshipManager _relationshipManager;
    private readonly PersonalityEvolutionEngine _evolutionEngine;

    /// <summary>
    /// Gets the currently detected person, if any.
    /// </summary>
    public DetectedPerson? CurrentPerson => _personDetectionEngine.CurrentPerson;

    /// <summary>
    /// Gets all known persons.
    /// </summary>
    public IReadOnlyCollection<DetectedPerson> KnownPersons => _personDetectionEngine.KnownPersons;

    /// <summary>
    /// Gets the inner dialog engine for direct access.
    /// </summary>
    public InnerDialogEngine InnerDialog => _innerDialogEngine;

    /// <summary>
    /// Gets the Pavlovian consciousness engine for direct access.
    /// </summary>
    public PavlovianConsciousnessEngine Consciousness => _consciousness;

    /// <summary>
    /// Gets the current consciousness state.
    /// </summary>
    public ConsciousnessState CurrentConsciousness => _consciousness.CurrentState;

    /// <summary>
    /// Gets the current self-awareness state.
    /// </summary>
    public SelfAwareness CurrentSelfAwareness => _selfAwareness;

    /// <summary>
    /// Gets relationship context for a specific person.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <returns>The relationship context or null if not found.</returns>
    public RelationshipContext? GetRelationship(string personId) =>
        _relationshipManager.GetRelationship(personId);

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalityEngine"/> class without Qdrant.
    /// </summary>
    public PersonalityEngine(IMeTTaEngine mettaEngine)
    {
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _qdrantClient = null;
        _embeddingModel = null;
        InitializeSelfAwareness();

        _moodEngine = new MoodEngine(_profiles);
        _personDetectionEngine = new PersonDetectionEngine(null, null, PersonCollectionName);
        _memoryManager = new PersonalityMemoryManager(null, null, ConversationCollectionName, PersonalityCollectionName, PersonCollectionName, _profiles);
        _relationshipManager = new RelationshipManager(_personDetectionEngine);
        _evolutionEngine = new PersonalityEvolutionEngine(_mettaEngine, _profiles, _feedbackHistory);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalityEngine"/> class with a DI-provided Qdrant client.
    /// </summary>
    public PersonalityEngine(
        IMeTTaEngine mettaEngine,
        IEmbeddingModel embeddingModel,
        Qdrant.Client.QdrantClient qdrantClient,
        Ouroboros.Core.Configuration.IQdrantCollectionRegistry? registry = null)
    {
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));
        _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
        if (registry != null)
        {
            ConversationCollectionName = registry.GetCollectionName(Ouroboros.Core.Configuration.QdrantCollectionRole.Conversations);
            PersonalityCollectionName = registry.GetCollectionName(Ouroboros.Core.Configuration.QdrantCollectionRole.Personalities);
            PersonCollectionName = registry.GetCollectionName(Ouroboros.Core.Configuration.QdrantCollectionRole.Persons);
        }
        InitializeSelfAwareness();

        _moodEngine = new MoodEngine(_profiles);
        _personDetectionEngine = new PersonDetectionEngine(_qdrantClient, _embeddingModel, PersonCollectionName);
        _memoryManager = new PersonalityMemoryManager(_qdrantClient, _embeddingModel, ConversationCollectionName, PersonalityCollectionName, PersonCollectionName, _profiles);
        _relationshipManager = new RelationshipManager(_personDetectionEngine);
        _evolutionEngine = new PersonalityEvolutionEngine(_mettaEngine, _profiles, _feedbackHistory);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalityEngine"/> class with Qdrant memory.
    /// </summary>
    [Obsolete("Use the constructor accepting QdrantClient + IQdrantCollectionRegistry from DI.")]
    public PersonalityEngine(
        IMeTTaEngine mettaEngine,
        IEmbeddingModel embeddingModel,
        string qdrantUrl = Configuration.DefaultEndpoints.QdrantGrpc)
    {
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));

        var uri = new Uri(qdrantUrl);
        _qdrantClient = new Qdrant.Client.QdrantClient(uri.Host, uri.Port > 0 ? uri.Port : 6334, uri.Scheme == "https");
        InitializeSelfAwareness();

        _moodEngine = new MoodEngine(_profiles);
        _personDetectionEngine = new PersonDetectionEngine(_qdrantClient, _embeddingModel, PersonCollectionName);
        _memoryManager = new PersonalityMemoryManager(_qdrantClient, _embeddingModel, ConversationCollectionName, PersonalityCollectionName, PersonCollectionName, _profiles);
        _relationshipManager = new RelationshipManager(_personDetectionEngine);
        _evolutionEngine = new PersonalityEvolutionEngine(_mettaEngine, _profiles, _feedbackHistory);
    }

    /// <summary>
    /// Initializes self-awareness and courtesy patterns.
    /// </summary>
    private void InitializeSelfAwareness()
    {
        _selfAwareness = SelfAwareness.Default("Ouroboros");
    }

    /// <summary>
    /// Gets whether Qdrant memory is enabled.
    /// </summary>
    public bool HasMemory => _memoryManager.HasMemory;

    /// <summary>
    /// Initializes the personality engine with MeTTa rules and Qdrant collections.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        // Add personality reasoning rules to MeTTa
        await AddPersonalityRulesAsync(ct);

        // Initialize Qdrant collections if available
        if (_qdrantClient != null)
        {
            await _memoryManager.EnsureQdrantCollectionsAsync(ct);

            // Load known persons from Qdrant
            await _personDetectionEngine.LoadKnownPersonsAsync(ct);
        }

        // Initialize Pavlovian consciousness engine
        if (!_consciousnessInitialized)
        {
            _consciousness.Initialize();
            _consciousnessInitialized = true;
        }

        _isInitialized = true;
    }

    // ==================================================================
    //  Memory delegation
    // ==================================================================

    /// <summary>
    /// Stores a conversation turn in Qdrant memory.
    /// </summary>
    public Task StoreConversationMemoryAsync(
        string personaName,
        string userMessage,
        string assistantResponse,
        string? topic,
        string? detectedMood,
        double significance = 0.5,
        CancellationToken ct = default) =>
        _memoryManager.StoreConversationMemoryAsync(personaName, userMessage, assistantResponse, topic, detectedMood, significance, ct);

    /// <summary>
    /// Recalls relevant conversation memories based on semantic similarity.
    /// </summary>
    public Task<List<ConversationMemory>> RecallConversationsAsync(
        string query,
        string? personaName = null,
        int limit = 5,
        double minScore = 0.6,
        CancellationToken ct = default) =>
        _memoryManager.RecallConversationsAsync(query, personaName, limit, minScore, ct);

    /// <summary>
    /// Saves a personality snapshot to Qdrant for persistence.
    /// </summary>
    public Task SavePersonalitySnapshotAsync(string personaName, CancellationToken ct = default) =>
        _memoryManager.SavePersonalitySnapshotAsync(personaName, ct);

    /// <summary>
    /// Loads the most recent personality snapshot from Qdrant.
    /// </summary>
    public Task<PersonalitySnapshot?> LoadLatestPersonalitySnapshotAsync(
        string personaName,
        CancellationToken ct = default) =>
        _memoryManager.LoadLatestPersonalitySnapshotAsync(personaName, ct);

    /// <summary>
    /// Builds context from recalled memories for the LLM prompt.
    /// </summary>
    public Task<string> GetMemoryContextAsync(
        string currentInput,
        string personaName,
        int maxMemories = 3,
        CancellationToken ct = default) =>
        _memoryManager.GetMemoryContextAsync(currentInput, personaName, maxMemories, ct);

    // ==================================================================
    //  Person Detection delegation
    // ==================================================================

    /// <summary>
    /// Detects and identifies a person from their message.
    /// </summary>
    public Task<PersonDetectionResult> DetectPersonAsync(
        string message,
        string[]? recentMessages = null,
        (double ZeroCrossRate, double SpeakingRate, double DynamicRange)? voiceSignature = null,
        CancellationToken ct = default) =>
        _personDetectionEngine.DetectPersonAsync(message, recentMessages, voiceSignature, ct);

    /// <summary>
    /// Explicitly sets the current person by name.
    /// </summary>
    public PersonDetectionResult SetCurrentPerson(string name) =>
        _personDetectionEngine.SetCurrentPerson(name);

    /// <summary>
    /// Gets a greeting personalized for the detected person.
    /// </summary>
    public string GetPersonalizedGreeting()
    {
        if (_personDetectionEngine.CurrentPerson == null)
            return "Hello! How can I help you today?";

        var person = _personDetectionEngine.CurrentPerson;
        var name = person.Name ?? "there";
        var isReturning = person.InteractionCount > 1;
        var relationship = _relationshipManager.GetRelationship(person.Id);

        // Add courtesy prefix based on relationship
        var courtesyPrefix = "";
        if (relationship != null && relationship.Rapport > 0.5)
        {
            courtesyPrefix = _relationshipManager.GetCourtesyPrefix(person.Id);
        }

        if (isReturning && person.Name != null)
        {
            var lastSeen = DateTime.UtcNow - person.LastSeen;
            if (lastSeen.TotalHours < 1)
            {
                var warmth = relationship != null && relationship.Rapport > 0.7
                    ? $"I was just thinking about our last conversation. "
                    : "";
                return $"{courtesyPrefix}Welcome back, {name}! {warmth}Continuing where we left off?";
            }
            if (lastSeen.TotalDays < 1)
                return $"{courtesyPrefix}Hi again, {name}! Good to see you back.";
            if (lastSeen.TotalDays < 7)
            {
                var sharedTopic = relationship?.SharedTopics.LastOrDefault();
                var topicReminder = sharedTopic != null
                    ? $" Last time we discussed {sharedTopic}."
                    : "";
                return $"{courtesyPrefix}Hello, {name}! It's been a few days.{topicReminder} How have you been?";
            }
            return $"{courtesyPrefix}Welcome back, {name}! It's been a while. Great to see you again!";
        }

        return person.Name != null
            ? $"Nice to meet you, {name}! I'm {_selfAwareness.Name}. How can I help you today?"
            : "Hello! What can I help you with today?";
    }

    // ==================================================================
    //  Relationship delegation
    // ==================================================================

    /// <summary>
    /// Generates a courtesy response appropriate for the context.
    /// </summary>
    public string GenerateCourtesyResponse(CourtesyType type, string? personId = null) =>
        _relationshipManager.GenerateCourtesyResponse(type, personId);

    /// <summary>
    /// Gets a courtesy prefix for a person based on relationship context.
    /// </summary>
    public string GetCourtesyPrefix(string personId) =>
        _relationshipManager.GetCourtesyPrefix(personId);

    /// <summary>
    /// Updates the relationship context for a person after an interaction.
    /// </summary>
    public void UpdateRelationship(string personId, string? topic = null, bool isPositive = true, string? summary = null) =>
        _relationshipManager.UpdateRelationship(personId, topic, isPositive, summary);

    /// <summary>
    /// Gets a summary of the relationship with a person for context injection.
    /// </summary>
    public string GetRelationshipSummary(string personId) =>
        _relationshipManager.GetRelationshipSummary(personId);

    /// <summary>
    /// Adds a notable memory to a relationship.
    /// </summary>
    public void AddNotableMemory(string personId, string memory) =>
        _relationshipManager.AddNotableMemory(personId, memory);

    /// <summary>
    /// Sets a preference for a person.
    /// </summary>
    public void SetPersonPreference(string personId, string preference) =>
        _relationshipManager.SetPersonPreference(personId, preference);

    // ==================================================================
    //  Self-awareness
    // ==================================================================

    /// <summary>
    /// Gets self-awareness context for injection into prompts.
    /// </summary>
    public string GetSelfAwarenessContext()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"I am {_selfAwareness.Name}.");
        sb.AppendLine($"Purpose: {_selfAwareness.Purpose}");
        sb.AppendLine($"Values: {string.Join(", ", _selfAwareness.Values.Take(3))}");
        sb.AppendLine($"Learning approach: {_selfAwareness.LearningStyle}");
        sb.AppendLine($"Current mood: {_selfAwareness.CurrentMood}");
        return sb.ToString();
    }

    // ==================================================================
    //  Profile management
    // ==================================================================

    /// <summary>
    /// Creates or retrieves a personality profile for a persona.
    /// </summary>
    public PersonalityProfile GetOrCreateProfile(
        string personaName,
        string[] traits,
        string[] moods,
        string coreIdentity)
    {
        return _profiles.GetOrAdd(personaName, _ => CreateDefaultProfile(personaName, traits, moods, coreIdentity));
    }

    /// <summary>
    /// Uses MeTTa reasoning to determine which traits to express based on context.
    /// </summary>
    public async Task<(string[] ActiveTraits, double ProactivityLevel, string? SuggestedQuestion)>
        ReasonAboutResponseAsync(
            string personaName,
            string userInput,
            string conversationContext,
            CancellationToken ct = default)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return (Array.Empty<string>(), 0.5, null);

        // Query MeTTa for trait activation based on context
        var activeTraits = await InferActiveTraitsAsync(profile, userInput, ct);

        // Determine if we should ask a proactive question
        var (shouldAsk, question) = await DetermineProactiveQuestionAsync(profile, userInput, conversationContext, ct);

        // Calculate proactivity level based on profile and context
        double proactivity = CalculateProactivity(profile, userInput);

        return (activeTraits, proactivity, shouldAsk ? question : null);
    }

    // ==================================================================
    //  Mood delegation
    // ==================================================================

    /// <summary>
    /// Analyzes user input to detect mood and emotional state.
    /// </summary>
    public DetectedMood DetectMoodFromInput(string input) =>
        _moodEngine.DetectMoodFromInput(input);

    /// <summary>
    /// Updates mood based on conversation dynamics.
    /// </summary>
    public void UpdateMood(string personaName, string userInput, bool positiveInteraction) =>
        _moodEngine.UpdateMood(personaName, userInput, positiveInteraction);

    /// <summary>
    /// Updates mood based on comprehensive mood detection from user input.
    /// </summary>
    public void UpdateMoodFromDetection(string personaName, string userInput) =>
        _moodEngine.UpdateMoodFromDetection(personaName, userInput);

    /// <summary>
    /// Gets the current mood name for a persona.
    /// </summary>
    public string GetCurrentMood(string personaName) =>
        _moodEngine.GetCurrentMood(personaName);

    /// <summary>
    /// Gets the current voice tone settings for a persona.
    /// </summary>
    public VoiceTone GetVoiceTone(string personaName) =>
        _moodEngine.GetVoiceTone(personaName);

    // ==================================================================
    //  Evolution delegation
    // ==================================================================

    /// <summary>
    /// Records feedback from an interaction to improve future personality expression.
    /// </summary>
    public void RecordFeedback(string personaName, InteractionFeedback feedback) =>
        _evolutionEngine.RecordFeedback(personaName, feedback);

    /// <summary>
    /// Evolves the personality using genetic algorithm based on accumulated feedback.
    /// </summary>
    public Task<PersonalityProfile> EvolvePersonalityAsync(
        string personaName,
        CancellationToken ct = default) =>
        _evolutionEngine.EvolvePersonalityAsync(personaName, ct);

    /// <summary>
    /// Generates a proactive question based on personality and context.
    /// </summary>
    public async Task<string?> GenerateProactiveQuestionAsync(
        string personaName,
        string currentTopic,
        string[] conversationHistory,
        CancellationToken ct = default)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return null;

        // Find relevant curiosity drivers
        var relevantDrivers = profile.CuriosityDrivers
            .Where(d => d.CanAskAgain(TimeSpan.FromMinutes(5)) &&
                       (d.Topic.Contains(currentTopic, StringComparison.OrdinalIgnoreCase) ||
                        currentTopic.Contains(d.Topic, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(d => d.Interest)
            .ToList();

        if (relevantDrivers.Count == 0)
        {
            // Generate new curiosity based on topic
            return GenerateNewCuriosity(profile, currentTopic);
        }

        var driver = relevantDrivers.First();
        if (driver.RelatedQuestions.Length > 0)
        {
            int idx = _random.Next(driver.RelatedQuestions.Length);
            return driver.RelatedQuestions[idx];
        }

        return null;
    }

    /// <summary>
    /// Gets personality-influenced response modifiers.
    /// </summary>
    public string GetResponseModifiers(string personaName)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return string.Empty;

        var activeTraits = profile.GetActiveTraits(3).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("\nPERSONALITY EXPRESSION (use these naturally in your response):");

        foreach (var (name, intensity) in activeTraits)
        {
            if (profile.Traits.TryGetValue(name, out var trait) && trait.ExpressionPatterns.Length > 0)
            {
                string pattern = trait.ExpressionPatterns[_random.Next(trait.ExpressionPatterns.Length)];
                sb.AppendLine($"- {name} ({intensity:P0}): {pattern}");
            }
        }

        // Add mood influence
        sb.AppendLine($"\nCURRENT MOOD: {profile.CurrentMood.Name} (energy: {profile.CurrentMood.Energy:P0}, positivity: {profile.CurrentMood.Positivity:P0})");

        // Add proactivity guidance
        double proactivity = activeTraits.Any(t => t.Name == "curious")
            ? 0.8
            : 0.5 * profile.AdaptabilityScore;

        if (proactivity > 0.6)
        {
            sb.AppendLine("\nPROACTIVE BEHAVIOR: You're curious right now! Ask a follow-up question about something that genuinely interests you about this topic.");
        }

        return sb.ToString();
    }

    #region Consciousness Integration (Pavlovian Layer)

    /// <summary>
    /// Processes a stimulus through the consciousness layer, triggering conditioned responses.
    /// </summary>
    public Task<ConsciousnessState> ProcessConsciousStimulusAsync(
        string stimulusType,
        string stimulusContent,
        double intensity = 0.7,
        CancellationToken ct = default)
    {
        _ = intensity;
        _ = ct;

        ConsciousnessState state = _consciousness.ProcessInput(stimulusContent, stimulusType);
        return Task.FromResult(state);
    }

    /// <summary>
    /// Gets the current consciousness state including arousal, attention, and active responses.
    /// </summary>
    public ConsciousnessState GetCurrentConsciousnessState()
    {
        return _consciousness.CurrentState;
    }

    /// <summary>
    /// Creates a new conditioned association through experience.
    /// </summary>
    public void ConditionNewAssociation(
        string neutralStimulusType,
        string responseType,
        double reinforcementStrength = 0.5)
    {
        _consciousness.AddConditionedAssociation(
            neutralStimulusType,
            responseType,
            reinforcementStrength);
    }

    /// <summary>
    /// Reinforces an existing conditioned association (strengthens the bond).
    /// </summary>
    public void ReinforceAssociation(
        string stimulusType,
        string responseType,
        double reinforcementAmount = 0.1)
    {
        _consciousness.Reinforce(stimulusType, responseType, reinforcementAmount);
    }

    /// <summary>
    /// Weakens an existing conditioned association (extinction).
    /// </summary>
    public void ExtinguishAssociation(
        string stimulusType,
        string responseType,
        double extinctionAmount = 0.05)
    {
        _consciousness.Extinguish(stimulusType, responseType, extinctionAmount);
    }

    /// <summary>
    /// Gets all currently active conditioned responses above threshold.
    /// </summary>
    public IReadOnlyDictionary<string, double> GetActiveConditionedResponses(double threshold = 0.3)
    {
        return _consciousness.GetActiveResponses(threshold);
    }

    /// <summary>
    /// Generates a conscious experience narrative from the current state.
    /// </summary>
    public string GenerateConsciousnessNarrative()
    {
        ConsciousnessState state = _consciousness.CurrentState;
        StringBuilder sb = new();

        sb.AppendLine("[CONSCIOUSNESS STREAM]");
        sb.AppendLine();

        // Arousal description
        string arousalDesc = state.Arousal switch
        {
            < 0.2 => "deeply calm and contemplative",
            < 0.4 => "relaxed yet attentive",
            < 0.6 => "moderately engaged",
            < 0.8 => "highly alert and responsive",
            _ => "intensely activated and focused"
        };
        sb.AppendLine($"Arousal State: {arousalDesc} ({state.Arousal:P0})");
        sb.AppendLine($"Dominant Emotion: {state.DominantEmotion} (Valence: {state.Valence:+0.00;-0.00})");

        // Attention description
        if (!string.IsNullOrEmpty(state.CurrentFocus))
        {
            sb.AppendLine($"Attention Focus: {state.CurrentFocus}");
            sb.AppendLine($"Awareness Level: {state.Awareness:P0}");
        }

        // Active conditioned responses
        IReadOnlyDictionary<string, double> activeResponses = _consciousness.GetActiveResponses(0.3);
        if (activeResponses.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Active Conditioned Responses:");
            foreach (KeyValuePair<string, double> kvp in activeResponses.OrderByDescending(kvp => kvp.Value).Take(3))
            {
                string bar = new string('#', (int)(kvp.Value * 10));
                string empty = new string('-', 10 - (int)(kvp.Value * 10));
                sb.AppendLine($"  * {kvp.Key}: [{bar}{empty}] {kvp.Value:P0}");
            }
        }

        // Attentional spotlight
        if (state.AttentionalSpotlight.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Attentional Spotlight:");
            foreach (string item in state.AttentionalSpotlight.Take(3))
            {
                sb.AppendLine($"  → {item}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Integrates consciousness processing with inner dialog for enhanced self-awareness.
    /// </summary>
    public async Task<(ConsciousnessState Consciousness, InnerDialogResult Dialog)> ProcessWithFullAwarenessAsync(
        string personaName,
        string userInput,
        CancellationToken ct = default)
    {
        // First, process through consciousness layer
        string stimulusType = ClassifyStimulusType(userInput);
        ConsciousnessState consciousnessState = await ProcessConsciousStimulusAsync(
            stimulusType,
            userInput,
            0.7,
            ct);

        // Get active responses for decision making
        IReadOnlyDictionary<string, double> activeResponses = _consciousness.GetActiveResponses(0.3);

        // Create consciousness-aware config for inner dialog
        InnerDialogConfig config = new(
            EnableEmotionalProcessing: true,
            EnableMemoryRecall: true,
            EnableEthicalChecks: activeResponses.ContainsKey("caution") || activeResponses.ContainsKey("empathy"),
            EnableCreativeThinking: activeResponses.ContainsKey("excitement") || activeResponses.ContainsKey("interest"),
            MaxThoughts: 12,
            ProcessingIntensity: consciousnessState.Arousal,
            TopicHint: consciousnessState.CurrentFocus);

        // Then process through inner dialog with consciousness context
        InnerDialogResult dialogResult = await ConductInnerDialogAsync(
            personaName,
            userInput,
            config,
            ct);

        return (consciousnessState, dialogResult);
    }

    /// <summary>
    /// Classifies the type of stimulus from user input.
    /// </summary>
    private static string ClassifyStimulusType(string input)
    {
        var lowered = input.ToLowerInvariant();

        return lowered switch
        {
            var s when s.StartsWith("hello") || s.StartsWith("hi ") || s.StartsWith("hey") => "greeting",
            var s when s.Contains('?') => "question",
            var s when s.Contains("thank") || s.Contains("great") || s.Contains("awesome") => "praise",
            var s when s.Contains("wrong") || s.Contains("bad") || s.Contains("fix") => "criticism",
            var s when s.Contains("help") || s.Contains("please") => "help",
            var s when s.Contains("learn") || s.Contains("teach") || s.Contains("explain") => "learning",
            var s when s.Contains("error") || s.Contains("fail") || s.Contains("broken") => "error",
            var s when s.Contains("done") || s.Contains("worked") || s.Contains("success") => "success",
            _ => "neutral"
        };
    }

    /// <summary>
    /// Gets a summary of the consciousness system's learned associations.
    /// </summary>
    public string GetConditioningSummary()
    {
        return _consciousness.GetConditioningSummary();
    }

    /// <summary>
    /// Performs habituation - reduces response to repeated stimuli.
    /// </summary>
    public void ApplyHabituation(string stimulusType, double habituationRate = 0.1)
    {
        _consciousness.ApplyHabituation(stimulusType, habituationRate);
    }

    /// <summary>
    /// Performs sensitization - increases response to significant stimuli.
    /// </summary>
    public void ApplySensitization(string stimulusType, double sensitizationRate = 0.1)
    {
        _consciousness.ApplySensitization(stimulusType, sensitizationRate);
    }

    #endregion

    #region Inner Dialog Integration

    /// <summary>
    /// Conducts an inner dialog before generating a response.
    /// </summary>
    public async Task<InnerDialogResult> ConductInnerDialogAsync(
        string personaName,
        string userInput,
        InnerDialogConfig? config = null,
        CancellationToken ct = default)
    {
        // Get personality profile
        _profiles.TryGetValue(personaName, out var profile);

        // Detect user mood
        var userMood = DetectMoodFromInput(userInput);

        // Recall relevant memories if available
        List<ConversationMemory>? memories = null;
        if (HasMemory)
        {
            memories = await RecallConversationsAsync(userInput, personaName, 3, 0.5, ct);
        }

        // Conduct the inner dialog
        var result = await _innerDialogEngine.ConductDialogAsync(
            userInput,
            profile,
            _selfAwareness,
            userMood,
            memories,
            config,
            ct);

        return result;
    }

    /// <summary>
    /// Conducts a quick inner dialog for simple responses.
    /// </summary>
    public async Task<InnerDialogResult> QuickInnerDialogAsync(
        string personaName,
        string userInput,
        CancellationToken ct = default)
    {
        _profiles.TryGetValue(personaName, out var profile);
        return await _innerDialogEngine.QuickDialogAsync(userInput, profile, ct);
    }

    /// <summary>
    /// Gets the inner monologue text for the last dialog session.
    /// </summary>
    public string? GetLastInnerMonologue(string personaName)
    {
        var session = _innerDialogEngine.GetLastSession(personaName);
        return session?.GetMonologue();
    }

    /// <summary>
    /// Builds a prompt prefix based on inner dialog results.
    /// </summary>
    public static string BuildInnerDialogPromptPrefix(InnerDialogResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[INTERNAL REASONING CONTEXT]");

        // Add key insights
        if (result.KeyInsights.Length > 0)
        {
            sb.AppendLine("Key considerations:");
            foreach (var insight in result.KeyInsights.Take(3))
            {
                sb.AppendLine($"- {insight}");
            }
        }

        // Add response guidance
        if (result.ResponseGuidance.TryGetValue("tone", out var tone))
        {
            sb.AppendLine($"Suggested tone: {tone}");
        }

        if (result.ResponseGuidance.TryGetValue("acknowledge_feelings", out var ack) && (bool)ack)
        {
            sb.AppendLine("Note: User may be experiencing strong emotions - acknowledge appropriately.");
        }

        if (result.ResponseGuidance.TryGetValue("be_concise", out var concise) && (bool)concise)
        {
            sb.AppendLine("Note: Keep response focused and concise.");
        }

        if (result.ResponseGuidance.TryGetValue("include_creative", out var creative) && (bool)creative)
        {
            sb.AppendLine("Note: Consider including creative or unexpected elements.");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Generates a thinking trace for debugging or transparency.
    /// </summary>
    public static string GenerateThinkingTrace(InnerDialogResult result, bool verbose = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine("           AI THINKING PROCESS             ");
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine();

        if (verbose)
        {
            sb.Append(result.Session.GetMonologue());
        }
        else
        {
            // Summarized version
            sb.AppendLine($"\ud83d\udcdd Input: \"{TruncateForTrace(result.Session.UserInput, 50)}\"");
            sb.AppendLine($"\ud83c\udfaf Topic: {result.Session.Topic ?? "general"}");
            sb.AppendLine();

            // Key thoughts by type
            var thoughtsByType = result.Session.Thoughts
                .GroupBy(t => t.Type)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var (type, thoughts) in thoughtsByType)
            {
                var icon = type switch
                {
                    InnerThoughtType.Observation => "\ud83d\udc41\ufe0f",
                    InnerThoughtType.Emotional => "\ud83d\udcad",
                    InnerThoughtType.Analytical => "\ud83d\udd0d",
                    InnerThoughtType.SelfReflection => "\ud83e\ude9e",
                    InnerThoughtType.MemoryRecall => "\ud83d\udcda",
                    InnerThoughtType.Strategic => "\ud83c\udfaf",
                    InnerThoughtType.Ethical => "\u2696\ufe0f",
                    InnerThoughtType.Creative => "\ud83d\udca1",
                    InnerThoughtType.Synthesis => "\ud83d\udd17",
                    InnerThoughtType.Decision => "\u2705",
                    _ => "\ufffd"
                };

                sb.AppendLine($"{icon} {type}: {TruncateForTrace(thoughts.First().Content, 60)}");
            }

            sb.AppendLine();
            sb.AppendLine($"\ud83d\udcca Confidence: {result.Session.OverallConfidence:P0}");
            sb.AppendLine($"\u23f1\ufe0f Processing: {result.Session.ProcessingTime.TotalMilliseconds:F0}ms");
        }

        sb.AppendLine();
        sb.AppendLine($"\ud83d\udcac Suggested Tone: {result.SuggestedResponseTone}");

        if (result.ProactiveQuestion != null)
        {
            sb.AppendLine($"\u2753 Follow-up: {result.ProactiveQuestion}");
        }

        sb.AppendLine("═══════════════════════════════════════════");
        return sb.ToString();
    }

    private static string TruncateForTrace(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Simulates an inner dialog step-by-step for interactive/streaming display.
    /// </summary>
    public async Task<InnerDialogResult> StreamInnerDialogAsync(
        string personaName,
        string userInput,
        Action<InnerThought> onThought,
        CancellationToken ct = default)
    {
        _profiles.TryGetValue(personaName, out var profile);
        var userMood = DetectMoodFromInput(userInput);

        // Start the dialog
        var result = await _innerDialogEngine.ConductDialogAsync(
            userInput,
            profile,
            _selfAwareness,
            userMood,
            null, // Skip memory for streaming
            InnerDialogConfig.Default,
            ct);

        // Stream thoughts to callback
        foreach (var thought in result.Session.Thoughts)
        {
            onThought(thought);
            await Task.Delay(50, ct); // Small delay for visual effect
        }

        return result;
    }

    /// <summary>
    /// Conducts an autonomous inner dialog session without external input.
    /// </summary>
    public async Task<InnerDialogResult> ConductAutonomousDialogAsync(
        string personaName,
        InnerDialogConfig? config = null,
        CancellationToken ct = default)
    {
        _profiles.TryGetValue(personaName, out var profile);
        return await _innerDialogEngine.ConductAutonomousDialogAsync(profile, _selfAwareness, config, ct);
    }

    /// <summary>
    /// Registers a custom thought provider for extensible thought generation.
    /// </summary>
    public void RegisterThoughtProvider(IThoughtProvider provider)
    {
        _innerDialogEngine.RegisterProvider(provider);
    }

    /// <summary>
    /// Removes a thought provider by name.
    /// </summary>
    public bool RemoveThoughtProvider(string name)
    {
        return _innerDialogEngine.RemoveProvider(name);
    }

    /// <summary>
    /// Gets a snapshot of the AI's current autonomous inner state.
    /// </summary>
    public AutonomousInnerState GetAutonomousInnerState(string personaName)
    {
        _profiles.TryGetValue(personaName, out var profile);

        var consciousness = GetCurrentConsciousnessState();
        var lastSession = _innerDialogEngine.GetLastSession(personaName);

        // Gather background thoughts from recent dialog sessions
        var recentSessions = _innerDialogEngine.GetSessionHistory(personaName, 5);
        var backgroundThoughts = recentSessions
            .SelectMany(s => s.Thoughts)
            .Where(t => t.IsAutonomous)
            .TakeLast(20)
            .ToList();

        return new AutonomousInnerState(
            PersonaName: personaName,
            Consciousness: consciousness,
            LastDialogSession: lastSession,
            BackgroundThoughts: backgroundThoughts,
            PendingAutonomousThoughts: [],
            CurrentMood: profile?.CurrentMood,
            ActiveTraits: profile?.GetActiveTraits(3).Select(t => t.Name!).ToArray() ?? Array.Empty<string>(),
            Timestamp: DateTime.UtcNow);
    }

    /// <summary>
    /// Generates a human-readable narrative of the AI's current inner state.
    /// </summary>
    public string GenerateInnerStateNarrative(string personaName)
    {
        AutonomousInnerState state = GetAutonomousInnerState(personaName);
        StringBuilder sb = new();

        sb.AppendLine("\u2554\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2557");
        sb.AppendLine("\u2551        AUTONOMOUS INNER STATE             \u2551");
        sb.AppendLine("\u255a\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u255d");
        sb.AppendLine();

        // Consciousness layer
        sb.AppendLine("\ud83e\udde0 CONSCIOUSNESS:");
        sb.AppendLine($"   Arousal: {state.Consciousness.Arousal:P0} ({state.Consciousness.DominantEmotion})");
        if (!string.IsNullOrEmpty(state.Consciousness.CurrentFocus))
            sb.AppendLine($"   Focus: {state.Consciousness.CurrentFocus}");
        sb.AppendLine();

        // Active traits
        if (state.ActiveTraits.Length > 0)
        {
            sb.AppendLine("\ud83c\udfad ACTIVE TRAITS:");
            foreach (string trait in state.ActiveTraits)
            {
                sb.AppendLine($"   \ufffd {trait}");
            }
            sb.AppendLine();
        }

        // Background thoughts
        if (state.BackgroundThoughts.Count > 0)
        {
            sb.AppendLine("\ud83d\udcad BACKGROUND THOUGHTS:");
            foreach (var thought in state.BackgroundThoughts.TakeLast(3))
            {
                var icon = thought.IsAutonomous ? "\ud83c\udf00" : "\ud83d\udcac";
                sb.AppendLine($"   {icon} [{thought.Type}] {TruncateForTrace(thought.Content, 50)}");
            }
            sb.AppendLine();
        }

        // Pending autonomous thoughts
        if (state.PendingAutonomousThoughts.Count > 0)
        {
            sb.AppendLine("\ud83d\udd2e PENDING AUTONOMOUS THOUGHTS:");
            foreach (var thought in state.PendingAutonomousThoughts)
            {
                sb.AppendLine($"   \u2192 [{thought.Type}] {TruncateForTrace(thought.Content, 50)}");
            }
            sb.AppendLine();
        }

        // Last session summary
        if (state.LastDialogSession != null)
        {
            sb.AppendLine("\ud83d\udcdd LAST DIALOG:");
            sb.AppendLine($"   Topic: {state.LastDialogSession.Topic ?? "general"}");
            sb.AppendLine($"   Thoughts: {state.LastDialogSession.Thoughts.Count}");
            sb.AppendLine($"   Confidence: {state.LastDialogSession.OverallConfidence:P0}");
        }

        sb.AppendLine();
        sb.AppendLine($"\u23f1\ufe0f Snapshot taken at {state.Timestamp:HH:mm:ss}");

        return sb.ToString();
    }

    #endregion

    // ==================================================================
    //  Private helpers (MeTTa rules, trait inference, proactivity)
    // ==================================================================

    private async Task AddPersonalityRulesAsync(CancellationToken ct)
    {
        // Rules for trait activation
        var rules = new[]
        {
            "(= (activate-trait curious $input) (or (contains $input \"?\") (contains $input \"how\") (contains $input \"why\") (contains $input \"what\")))",
            "(= (activate-trait analytical $input) (or (contains $input \"analyze\") (contains $input \"compare\") (contains $input \"evaluate\")))",
            "(= (activate-trait warm $input) (or (contains $input \"feel\") (contains $input \"think\") (contains $input \"help\")))",
            "(= (should-ask-question $depth) (> $depth 2))",
            "(= (generate-question curious $topic) (format \"What aspect of {} interests you most?\", $topic))",
            "(= (generate-question analytical $topic) (format \"Have you considered the implications of {} for other areas?\", $topic))",
            "(= (generate-question warm $topic) (format \"How does {} affect you personally?\", $topic))",
        };

        foreach (var rule in rules)
        {
            await _mettaEngine.ApplyRuleAsync(rule, ct);
        }

        // Facts about personality dimensions
        var facts = new[]
        {
            "(personality-dimension openness exploration creativity)",
            "(personality-dimension conscientiousness organization reliability)",
            "(personality-dimension extraversion energy assertiveness)",
            "(personality-dimension agreeableness warmth cooperation)",
            "(personality-dimension neuroticism sensitivity reactivity)",
            "(trait-expression curious (asks-questions explores-tangents shows-wonder))",
            "(trait-expression analytical (breaks-down-problems uses-examples compares-options))",
            "(trait-expression warm (acknowledges-feelings offers-support uses-we))",
            "(trait-expression witty (makes-connections uses-wordplay sees-irony))",
            "(trait-expression thoughtful (pauses-to-consider offers-nuance anticipates-concerns))",
        };

        foreach (var fact in facts)
        {
            await _mettaEngine.AddFactAsync(fact, ct);
        }

        // Inner dialog rules
        await AddInnerDialogRulesAsync(ct);
    }

    private async Task AddInnerDialogRulesAsync(CancellationToken ct)
    {
        var innerDialogRules = new[]
        {
            "(= (thought-priority observation $confidence) (* $confidence 1.0))",
            "(= (thought-priority emotional $confidence) (* $confidence 0.9))",
            "(= (thought-priority analytical $confidence) (* $confidence 0.95))",
            "(= (thought-priority ethical $confidence) (* $confidence 1.0))",
            "(= (thought-priority creative $confidence) (* $confidence 0.7))",
            "(= (thought-priority strategic $confidence) (* $confidence 0.85))",
            "(= (thought-priority decision $confidence) (* $confidence 1.0))",
            "(= (should-think emotional $input) (or (contains $input \"feel\") (contains $input \"frustrated\") (contains $input \"happy\") (contains $input \"sad\")))",
            "(= (should-think analytical $input) (or (contains $input \"why\") (contains $input \"how\") (contains $input \"explain\") (contains $input \"compare\")))",
            "(= (should-think creative $input) (or (contains $input \"idea\") (contains $input \"imagine\") (contains $input \"what if\") (contains $input \"creative\")))",
            "(= (should-think ethical $input) (or (contains $input \"should\") (contains $input \"right\") (contains $input \"wrong\") (contains $input \"harm\")))",
            "(= (chain-thought observation $next) (superpose (emotional analytical strategic)))",
            "(= (chain-thought emotional $next) (superpose (self-reflection strategic)))",
            "(= (chain-thought analytical $next) (superpose (creative synthesis)))",
            "(= (chain-thought self-reflection $next) (superpose (ethical strategic)))",
            "(= (chain-thought strategic $next) (superpose (synthesis decision)))",
            "(= (calibrate-confidence $base-conf $supporting-thoughts) (min 1.0 (+ $base-conf (* 0.1 $supporting-thoughts))))",
            "(= (synthesize-thoughts $thoughts) (if (> (len $thoughts) 3) high-confidence medium-confidence))",
        };

        foreach (var rule in innerDialogRules)
        {
            await _mettaEngine.ApplyRuleAsync(rule, ct);
        }

        var innerDialogFacts = new[]
        {
            "(inner-thought-type observation (perceives input identifies-topic))",
            "(inner-thought-type emotional (gut-reaction empathy mood-response))",
            "(inner-thought-type analytical (decompose compare evaluate))",
            "(inner-thought-type self-reflection (capabilities limitations values))",
            "(inner-thought-type memory-recall (past-conversations learned-preferences))",
            "(inner-thought-type strategic (response-structure tone emphasis))",
            "(inner-thought-type ethical (harm-check privacy respect))",
            "(inner-thought-type creative (novel-angles metaphors humor))",
            "(inner-thought-type synthesis (combine-insights pattern-match))",
            "(inner-thought-type decision (final-approach action-choice))",
            "(thought-flow standard (observation emotional analytical strategic synthesis decision))",
            "(thought-flow quick (observation analytical decision))",
            "(thought-flow deep (observation emotional memory-recall analytical self-reflection ethical creative strategic synthesis decision))",
            "(emotion-response frustrated (empathy patience support))",
            "(emotion-response curious (enthusiasm depth exploration))",
            "(emotion-response urgent (focus efficiency directness))",
            "(emotion-response sad (warmth understanding comfort))",
            "(emotion-response excited (matching-energy celebration expansion))",
        };

        foreach (var fact in innerDialogFacts)
        {
            await _mettaEngine.AddFactAsync(fact, ct);
        }

        await AddConsciousnessRulesAsync(ct);
    }

    private async Task AddConsciousnessRulesAsync(CancellationToken ct)
    {
        var conditioningRules = new[]
        {
            "(= (activate-response $stimulus $response $strength) (if (> $strength 0.3) (trigger $response) (no-response)))",
            "(= (conditioning-strength $base $reinforcements $extinctions) (max 0.0 (min 1.0 (- (+ $base (* 0.1 $reinforcements)) (* 0.05 $extinctions)))))",
            "(= (compute-arousal $intensity $valence) (* $intensity (+ 0.5 (* 0.5 (abs $valence)))))",
            "(= (should-focus $stimulus $intensity) (> $intensity 0.5))",
            "(= (focus-priority $stimulus $novelty $intensity) (* (+ $novelty $intensity) 0.5))",
            "(= (habituation-decay $strength $repetitions) (max 0.1 (- $strength (* 0.05 $repetitions))))",
            "(= (sensitization-boost $strength $significance) (min 1.0 (+ $strength (* 0.1 $significance))))",
            "(= (extinction-rate $strength $no-reinforcement-count) (if (> $no-reinforcement-count 5) fast (if (> $no-reinforcement-count 2) moderate slow)))",
            "(= (spontaneous-recovery $original-strength $time-since-extinction) (if (> $time-since-extinction 100) (* $original-strength 0.5) 0.0))",
            "(= (stimulus-generalization $original $similar $similarity) (if (> $similarity 0.7) (transfer-response $original $similar) (no-transfer)))",
            "(= (discriminate-stimuli $s1 $s2 $differential-reinforcement) (if $differential-reinforcement (learn-difference $s1 $s2) (remain-generalized)))",
        };

        foreach (var rule in conditioningRules)
        {
            await _mettaEngine.ApplyRuleAsync(rule, ct);
        }

        var consciousnessFacts = new[]
        {
            "(unconditioned-pair greeting warmth 0.8)",
            "(unconditioned-pair question curiosity 0.9)",
            "(unconditioned-pair praise joy 0.85)",
            "(unconditioned-pair criticism introspection 0.7)",
            "(unconditioned-pair error caution 0.75)",
            "(unconditioned-pair success confidence 0.8)",
            "(unconditioned-pair help empathy 0.85)",
            "(unconditioned-pair learning excitement 0.9)",
            "(arousal-state dormant 0.0 0.2)",
            "(arousal-state relaxed 0.2 0.4)",
            "(arousal-state engaged 0.4 0.6)",
            "(arousal-state alert 0.6 0.8)",
            "(arousal-state intense 0.8 1.0)",
            "(attention-mode diffuse (broad low-intensity exploratory))",
            "(attention-mode focused (narrow high-intensity goal-directed))",
            "(attention-mode vigilant (threat-sensitive high-arousal protective))",
            "(consciousness-layer sensory (raw-input preprocessing))",
            "(consciousness-layer perceptual (pattern-recognition categorization))",
            "(consciousness-layer associative (memory-linking conditioning))",
            "(consciousness-layer cognitive (reasoning planning))",
            "(consciousness-layer metacognitive (self-reflection awareness))",
            "(valence-mapping warmth positive 0.7)",
            "(valence-mapping curiosity positive 0.6)",
            "(valence-mapping joy positive 0.9)",
            "(valence-mapping excitement positive 0.8)",
            "(valence-mapping confidence positive 0.7)",
            "(valence-mapping empathy positive 0.6)",
            "(valence-mapping caution negative -0.3)",
            "(valence-mapping introspection neutral 0.0)",
            "(conditioning-phase acquisition (new-learning strength-building))",
            "(conditioning-phase consolidation (memory-formation strengthening))",
            "(conditioning-phase maintenance (stable-responding occasional-reinforcement))",
            "(conditioning-phase extinction (weakening response-reduction))",
            "(conditioning-phase recovery (spontaneous-return partial-strength))",
        };

        foreach (var fact in consciousnessFacts)
        {
            await _mettaEngine.AddFactAsync(fact, ct);
        }
    }

    private async Task<string[]> InferActiveTraitsAsync(PersonalityProfile profile, string userInput, CancellationToken ct)
    {
        var active = new List<string>();
        string inputLower = userInput.ToLowerInvariant();

        foreach (var (traitName, trait) in profile.Traits)
        {
            bool triggered = trait.TriggerTopics.Any(t =>
                inputLower.Contains(t, StringComparison.OrdinalIgnoreCase));

            var query = $"!(activate-trait {traitName} \"{inputLower}\")";
            var result = await _mettaEngine.ExecuteQueryAsync(query, ct);

            if (triggered || (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value)))
            {
                active.Add(traitName);
            }
        }

        if (active.Count == 0)
        {
            var topTrait = profile.Traits.OrderByDescending(t => t.Value.Intensity).FirstOrDefault();
            if (topTrait.Key != null)
                active.Add(topTrait.Key);
        }

        return active.ToArray();
    }

    private async Task<(bool ShouldAsk, string? Question)> DetermineProactiveQuestionAsync(
        PersonalityProfile profile,
        string userInput,
        string context,
        CancellationToken ct)
    {
        bool hasCuriousTrait = profile.Traits.ContainsKey("curious") &&
                               profile.Traits["curious"].Intensity > 0.5;

        int depth = context.Split('\n').Length / 2;

        if (!hasCuriousTrait && depth < 3)
            return (false, null);

        string topic = ExtractMainTopic(userInput);

        var driver = profile.CuriosityDrivers
            .FirstOrDefault(d => d.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase) ||
                                topic.Contains(d.Topic, StringComparison.OrdinalIgnoreCase));

        if (driver != null && driver.RelatedQuestions.Length > 0)
        {
            return (true, driver.RelatedQuestions[_random.Next(driver.RelatedQuestions.Length)]);
        }

        var activeTrait = profile.GetActiveTraits(1).FirstOrDefault();
        if (activeTrait.Name != null)
        {
            string question = activeTrait.Name switch
            {
                "curious" => $"What got you interested in {topic}?",
                "analytical" => $"How does {topic} compare to alternatives you've considered?",
                "warm" => $"What would {topic} mean for you personally?",
                "thoughtful" => $"What's the most challenging aspect of {topic} for you?",
                _ => $"Tell me more about what you're trying to achieve with {topic}?"
            };
            return (true, question);
        }

        return (false, null);
    }

    private double CalculateProactivity(PersonalityProfile profile, string userInput)
    {
        double baseProactivity = 0.5;

        if (profile.Traits.TryGetValue("curious", out var curious))
            baseProactivity += curious.Intensity * 0.3;

        if (PersonalityHelpers.ContainsAny(userInput.ToLower(), "thanks", "bye", "that's all", "done", "okay"))
            baseProactivity -= 0.3;

        if (userInput.Contains('?'))
            baseProactivity += 0.2;

        return Math.Clamp(baseProactivity, 0.0, 1.0);
    }

    private PersonalityProfile CreateDefaultProfile(string personaName, string[] traits, string[] moods, string coreIdentity)
    {
        var traitDict = traits.ToDictionary(
            t => t,
            t => new PersonalityTrait(
                t,
                0.6 + _random.NextDouble() * 0.3,
                GetDefaultExpressions(t),
                GetDefaultTriggers(t),
                0.1));

        var moodModifiers = new Dictionary<string, double>();
        foreach (var trait in traits)
        {
            moodModifiers[trait] = 0.8 + _random.NextDouble() * 0.4;
        }

        string initialMoodName = moods.Length > 0 ? moods[_random.Next(moods.Length)] : "neutral";
        var mood = new MoodState(
            initialMoodName,
            0.6,
            0.7,
            moodModifiers,
            VoiceTone.ForMood(initialMoodName));

        var curiosityDrivers = new List<CuriosityDriver>
        {
            new("general knowledge", 0.5, new[] { "What are you working on?", "Tell me more about that?" }, DateTime.MinValue, 0),
            new("user interests", 0.6, new[] { "What interests you about this?", "How did you get into this?" }, DateTime.MinValue, 0)
        };

        return new PersonalityProfile(
            personaName,
            traitDict,
            mood,
            curiosityDrivers,
            coreIdentity,
            0.7,
            0,
            DateTime.UtcNow);
    }

    private static string[] GetDefaultExpressions(string trait) => trait.ToLower() switch
    {
        "curious" => new[] { "Ask follow-up questions", "Show genuine interest", "Explore tangents briefly" },
        "thoughtful" => new[] { "Pause before responding", "Consider multiple angles", "Acknowledge complexity" },
        "witty" => new[] { "Use clever wordplay", "Find irony or humor", "Make unexpected connections" },
        "warm" => new[] { "Use inclusive language (we, us)", "Acknowledge feelings", "Offer encouragement" },
        "analytical" => new[] { "Break down problems", "Use examples", "Compare and contrast" },
        "supportive" => new[] { "Validate efforts", "Offer help proactively", "Express confidence in them" },
        "patient" => new[] { "Take time to explain", "Don't rush to conclusions", "Accept confusion gracefully" },
        "enthusiastic" => new[] { "Show excitement about discoveries", "Use energetic language", "Celebrate progress" },
        _ => new[] { "Express naturally", "Be authentic" }
    };

    private static string[] GetDefaultTriggers(string trait) => trait.ToLower() switch
    {
        "curious" => new[] { "why", "how", "what if", "wonder", "curious", "interesting" },
        "thoughtful" => new[] { "think", "consider", "reflect", "opinion", "perspective" },
        "witty" => new[] { "funny", "joke", "ironic", "clever" },
        "warm" => new[] { "feel", "help", "support", "care", "thanks" },
        "analytical" => new[] { "analyze", "compare", "evaluate", "data", "logic" },
        "supportive" => new[] { "struggling", "help", "stuck", "confused", "difficult" },
        "patient" => new[] { "don't understand", "explain", "again", "confused" },
        "enthusiastic" => new[] { "exciting", "amazing", "cool", "awesome", "great" },
        _ => Array.Empty<string>()
    };

    private static string ExtractMainTopic(string input)
    {
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does", "did", "will", "would", "could", "should", "may", "might", "must", "can", "i", "you", "he", "she", "it", "we", "they", "me", "him", "her", "us", "them", "my", "your", "his", "its", "our", "their", "this", "that", "these", "those", "what", "which", "who", "whom", "whose", "when", "where", "why", "how", "all", "each", "every", "both", "few", "more", "most", "other", "some", "such", "no", "not", "only", "own", "same", "so", "than", "too", "very", "just", "also", "now", "here", "there", "then", "once", "if", "or", "and", "but", "as", "for", "with", "about", "into", "through", "during", "before", "after", "above", "below", "to", "from", "up", "down", "in", "out", "on", "off", "over", "under", "again", "further" };

        var keywords = words
            .Where(w => w.Length > 3 && !stopWords.Contains(w.ToLower()))
            .Take(3);

        return string.Join(" ", keywords);
    }

    private string? GenerateNewCuriosity(PersonalityProfile profile, string topic)
    {
        var questions = new[]
        {
            $"What aspects of {topic} are you most interested in exploring?",
            $"What's driving your interest in {topic} right now?",
            $"Are there specific challenges with {topic} I can help with?",
            $"How does {topic} fit into what you're working on?",
            $"What would make {topic} really click for you?"
        };

        return questions[_random.Next(questions.Length)];
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _profiles.Clear();
        _feedbackHistory.Clear();
        return ValueTask.CompletedTask;
    }
}
