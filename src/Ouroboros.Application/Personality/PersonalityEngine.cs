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
public sealed partial class PersonalityEngine : IAsyncDisposable
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

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _profiles.Clear();
        _feedbackHistory.Clear();
        return ValueTask.CompletedTask;
    }
}
