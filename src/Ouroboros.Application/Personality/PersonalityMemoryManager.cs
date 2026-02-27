// <copyright file="PersonalityMemoryManager.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;
using System.Text;
using Ouroboros.Domain;

/// <summary>
/// Manages Qdrant-backed conversation memory, personality snapshots,
/// and collection lifecycle for the personality system.
/// </summary>
public sealed class PersonalityMemoryManager
{
    private readonly Qdrant.Client.QdrantClient? _qdrantClient;
    private readonly IEmbeddingModel? _embeddingModel;
    private readonly string _conversationCollectionName;
    private readonly string _personalityCollectionName;
    private readonly string _personCollectionName;
    private readonly ConcurrentDictionary<string, PersonalityProfile> _profiles;
    private int _vectorSize = 1536; // Will be detected from embedding model

    /// <summary>
    /// Gets whether Qdrant memory is enabled.
    /// </summary>
    public bool HasMemory => _qdrantClient != null && _embeddingModel != null;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalityMemoryManager"/> class.
    /// </summary>
    public PersonalityMemoryManager(
        Qdrant.Client.QdrantClient? qdrantClient,
        IEmbeddingModel? embeddingModel,
        string conversationCollectionName,
        string personalityCollectionName,
        string personCollectionName,
        ConcurrentDictionary<string, PersonalityProfile> profiles)
    {
        _qdrantClient = qdrantClient;
        _embeddingModel = embeddingModel;
        _conversationCollectionName = conversationCollectionName;
        _personalityCollectionName = personalityCollectionName;
        _personCollectionName = personCollectionName;
        _profiles = profiles;
    }

    /// <summary>
    /// Ensures Qdrant collections exist for conversation and personality storage.
    /// </summary>
    public async Task EnsureQdrantCollectionsAsync(CancellationToken ct)
    {
        if (_qdrantClient == null) return;

        try
        {
            // Detect vector size from embedding model
            if (_embeddingModel != null)
            {
                try
                {
                    var testEmbedding = await _embeddingModel.CreateEmbeddingsAsync("test", ct);
                    _vectorSize = testEmbedding.Length;
                    Console.WriteLine($"  [OK] Detected embedding dimension: {_vectorSize}");
                }
                catch
                {
                    Console.WriteLine($"  [~] Using default embedding dimension: {_vectorSize}");
                }
            }

            // Create conversation memory collection
            if (!await _qdrantClient.CollectionExistsAsync(_conversationCollectionName, ct))
            {
                await _qdrantClient.CreateCollectionAsync(
                    _conversationCollectionName,
                    new Qdrant.Client.Grpc.VectorParams { Size = (ulong)_vectorSize, Distance = Qdrant.Client.Grpc.Distance.Cosine },
                    cancellationToken: ct);
            }
            else
            {
                await RecreateCollectionIfDimensionMismatchAsync(_conversationCollectionName, ct);
            }

            // Create personality snapshot collection
            if (!await _qdrantClient.CollectionExistsAsync(_personalityCollectionName, ct))
            {
                await _qdrantClient.CreateCollectionAsync(
                    _personalityCollectionName,
                    new Qdrant.Client.Grpc.VectorParams { Size = (ulong)_vectorSize, Distance = Qdrant.Client.Grpc.Distance.Cosine },
                    cancellationToken: ct);
            }
            else
            {
                await RecreateCollectionIfDimensionMismatchAsync(_personalityCollectionName, ct);
            }

            // Create person detection collection
            if (!await _qdrantClient.CollectionExistsAsync(_personCollectionName, ct))
            {
                await _qdrantClient.CreateCollectionAsync(
                    _personCollectionName,
                    new Qdrant.Client.Grpc.VectorParams { Size = (ulong)_vectorSize, Distance = Qdrant.Client.Grpc.Distance.Cosine },
                    cancellationToken: ct);
            }
            else
            {
                await RecreateCollectionIfDimensionMismatchAsync(_personCollectionName, ct);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Qdrant collection init warning: {ex.Message}");
        }
    }

    /// <summary>
    /// Stores a conversation turn in Qdrant memory.
    /// </summary>
    public async Task StoreConversationMemoryAsync(
        string personaName,
        string userMessage,
        string assistantResponse,
        string? topic,
        string? detectedMood,
        double significance = 0.5,
        CancellationToken ct = default)
    {
        if (_qdrantClient == null || _embeddingModel == null) return;

        try
        {
            // Sanitize and truncate inputs to prevent encoding issues
            string safeUserMessage = PersonalityHelpers.SanitizeForEmbedding(userMessage, maxLength: 1000);
            string safeAssistantResponse = PersonalityHelpers.SanitizeForEmbedding(assistantResponse, maxLength: 2000);
            string safeTopic = PersonalityHelpers.SanitizeForEmbedding(topic ?? "general", maxLength: 100);

            var memory = new ConversationMemory(
                Id: Guid.NewGuid(),
                PersonaName: personaName,
                UserMessage: safeUserMessage,
                AssistantResponse: safeAssistantResponse,
                Topic: safeTopic,
                DetectedMood: detectedMood,
                Significance: significance,
                Keywords: PersonalityHelpers.ExtractKeywords(safeUserMessage + " " + safeAssistantResponse),
                Timestamp: DateTime.UtcNow);

            // Generate embedding for the conversation
            string searchText = PersonalityHelpers.SanitizeForEmbedding(memory.ToSearchText(), maxLength: 2000);
            float[] embedding = await _embeddingModel.CreateEmbeddingsAsync(searchText);

            // Ensure payload values are clean UTF-8
            string cleanUserMessage = PersonalityHelpers.CleanForPayload(safeUserMessage);
            string cleanAssistantResponse = PersonalityHelpers.CleanForPayload(safeAssistantResponse);

            // Create payload
            var payload = new Dictionary<string, Qdrant.Client.Grpc.Value>
            {
                ["persona_name"] = personaName,
                ["user_message"] = cleanUserMessage,
                ["assistant_response"] = cleanAssistantResponse,
                ["topic"] = safeTopic,
                ["mood"] = detectedMood ?? "neutral",
                ["significance"] = significance,
                ["keywords"] = string.Join(",", memory.Keywords),
                ["timestamp"] = memory.Timestamp.ToString("O")
            };

            var point = new Qdrant.Client.Grpc.PointStruct
            {
                Id = new Qdrant.Client.Grpc.PointId { Uuid = memory.Id.ToString() },
                Vectors = embedding.ToArray(),
                Payload = { payload }
            };

            await _qdrantClient.UpsertAsync(_conversationCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Failed to store conversation memory: {ex.Message}");
        }
    }

    /// <summary>
    /// Recalls relevant conversation memories based on semantic similarity.
    /// </summary>
    public async Task<List<ConversationMemory>> RecallConversationsAsync(
        string query,
        string? personaName = null,
        int limit = 5,
        double minScore = 0.6,
        CancellationToken ct = default)
    {
        if (_qdrantClient == null || _embeddingModel == null)
            return new List<ConversationMemory>();

        try
        {
            var queryEmbedding = await _embeddingModel.CreateEmbeddingsAsync(query);

            // Build filter for persona if specified
            Qdrant.Client.Grpc.Filter? filter = null;
            if (!string.IsNullOrEmpty(personaName))
            {
                filter = new Qdrant.Client.Grpc.Filter
                {
                    Must = { new Qdrant.Client.Grpc.Condition
                    {
                        Field = new Qdrant.Client.Grpc.FieldCondition
                        {
                            Key = "persona_name",
                            Match = new Qdrant.Client.Grpc.Match { Keyword = personaName }
                        }
                    }}
                };
            }

            var results = await _qdrantClient.SearchAsync(
                _conversationCollectionName,
                queryEmbedding.ToArray(),
                filter: filter,
                limit: (ulong)limit,
                scoreThreshold: (float)minScore,
                cancellationToken: ct);

            return results.Select(r =>
            {
                var payload = r.Payload;
                return new ConversationMemory(
                    Id: Guid.Parse(r.Id.Uuid),
                    PersonaName: payload.TryGetValue("persona_name", out var pn) ? pn.StringValue : "",
                    UserMessage: payload.TryGetValue("user_message", out var um) ? um.StringValue : "",
                    AssistantResponse: payload.TryGetValue("assistant_response", out var ar) ? ar.StringValue : "",
                    Topic: payload.TryGetValue("topic", out var t) ? t.StringValue : null,
                    DetectedMood: payload.TryGetValue("mood", out var m) ? m.StringValue : null,
                    Significance: payload.TryGetValue("significance", out var s) ? s.DoubleValue : 0.5,
                    Keywords: payload.TryGetValue("keywords", out var k) ? k.StringValue.Split(',') : Array.Empty<string>(),
                    Timestamp: payload.TryGetValue("timestamp", out var ts) && DateTime.TryParse(ts.StringValue, out var dt) ? dt : DateTime.UtcNow);
            }).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Failed to recall conversations: {ex.Message}");
            return new List<ConversationMemory>();
        }
    }

    /// <summary>
    /// Saves a personality snapshot to Qdrant for persistence.
    /// </summary>
    public async Task SavePersonalitySnapshotAsync(string personaName, CancellationToken ct = default)
    {
        if (_qdrantClient == null || _embeddingModel == null) return;
        if (!_profiles.TryGetValue(personaName, out var profile)) return;

        try
        {
            var snapshot = new PersonalitySnapshot(
                Id: Guid.NewGuid(),
                PersonaName: personaName,
                TraitIntensities: profile.Traits.ToDictionary(t => t.Key, t => t.Value.Intensity),
                CurrentMood: profile.CurrentMood.Name,
                AdaptabilityScore: profile.AdaptabilityScore,
                InteractionCount: profile.InteractionCount,
                Timestamp: DateTime.UtcNow);

            // Create searchable text for embedding
            var searchText = $"Persona: {personaName}, Mood: {snapshot.CurrentMood}, " +
                           $"Traits: {string.Join(", ", snapshot.TraitIntensities.Select(t => $"{t.Key}:{t.Value:F2}"))}";

            var embedding = await _embeddingModel.CreateEmbeddingsAsync(searchText);

            var payload = new Dictionary<string, Qdrant.Client.Grpc.Value>
            {
                ["persona_name"] = personaName,
                ["mood"] = snapshot.CurrentMood,
                ["adaptability"] = snapshot.AdaptabilityScore,
                ["interaction_count"] = snapshot.InteractionCount,
                ["traits_json"] = System.Text.Json.JsonSerializer.Serialize(snapshot.TraitIntensities),
                ["timestamp"] = snapshot.Timestamp.ToString("O")
            };

            var point = new Qdrant.Client.Grpc.PointStruct
            {
                Id = new Qdrant.Client.Grpc.PointId { Uuid = snapshot.Id.ToString() },
                Vectors = embedding.ToArray(),
                Payload = { payload }
            };

            await _qdrantClient.UpsertAsync(_personalityCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Failed to save personality snapshot: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the most recent personality snapshot from Qdrant.
    /// </summary>
    public async Task<PersonalitySnapshot?> LoadLatestPersonalitySnapshotAsync(
        string personaName,
        CancellationToken ct = default)
    {
        if (_qdrantClient == null || _embeddingModel == null) return null;

        try
        {
            var queryEmbedding = await _embeddingModel.CreateEmbeddingsAsync($"Persona: {personaName}");

            var filter = new Qdrant.Client.Grpc.Filter
            {
                Must = { new Qdrant.Client.Grpc.Condition
                {
                    Field = new Qdrant.Client.Grpc.FieldCondition
                    {
                        Key = "persona_name",
                        Match = new Qdrant.Client.Grpc.Match { Keyword = personaName }
                    }
                }}
            };

            var results = await _qdrantClient.SearchAsync(
                _personalityCollectionName,
                queryEmbedding.ToArray(),
                filter: filter,
                limit: 10,
                cancellationToken: ct);

            // Find the most recent one
            var latestResult = results
                .Select(r =>
                {
                    var payload = r.Payload;
                    var timestamp = payload.TryGetValue("timestamp", out var ts) && DateTime.TryParse(ts.StringValue, out var dt)
                        ? dt : DateTime.MinValue;
                    return (Result: r, Timestamp: timestamp);
                })
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefault();

            if (latestResult.Result == null) return null;

            var p = latestResult.Result.Payload;
            var traitsJson = p.TryGetValue("traits_json", out var tj) ? tj.StringValue : "{}";
            var traitIntensities = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(traitsJson)
                ?? new Dictionary<string, double>();

            return new PersonalitySnapshot(
                Id: Guid.Parse(latestResult.Result.Id.Uuid),
                PersonaName: personaName,
                TraitIntensities: traitIntensities,
                CurrentMood: p.TryGetValue("mood", out var m) ? m.StringValue : "neutral",
                AdaptabilityScore: p.TryGetValue("adaptability", out var a) ? a.DoubleValue : 0.5,
                InteractionCount: p.TryGetValue("interaction_count", out var ic) ? (int)ic.IntegerValue : 0,
                Timestamp: latestResult.Timestamp);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Failed to load personality snapshot: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Builds context from recalled memories for the LLM prompt.
    /// </summary>
    public async Task<string> GetMemoryContextAsync(
        string currentInput,
        string personaName,
        int maxMemories = 3,
        CancellationToken ct = default)
    {
        if (!HasMemory) return "";

        var memories = await RecallConversationsAsync(currentInput, personaName, maxMemories, 0.5, ct);

        if (memories.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine("\n[RELEVANT MEMORIES]");
        foreach (var mem in memories)
        {
            var age = DateTime.UtcNow - mem.Timestamp;
            var ageStr = age.TotalHours < 1 ? "recently" :
                        age.TotalDays < 1 ? $"{(int)age.TotalHours}h ago" :
                        $"{(int)age.TotalDays}d ago";
            sb.AppendLine($"- ({ageStr}) User asked about {mem.Topic ?? "topic"}: \"{PersonalityHelpers.TruncateText(mem.UserMessage, 100)}\"");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Recreates a collection if its vector dimension doesn't match the current embedding model.
    /// </summary>
    private async Task RecreateCollectionIfDimensionMismatchAsync(string collectionName, CancellationToken ct)
    {
        if (_qdrantClient == null) return;

        try
        {
            var info = await _qdrantClient.GetCollectionInfoAsync(collectionName, ct);
            var existingSize = info.Config?.Params?.VectorsConfig?.Params?.Size ?? 0;

            if (existingSize > 0 && existingSize != (ulong)_vectorSize)
            {
                Console.WriteLine($"  [!] Collection {collectionName} has dimension {existingSize}, expected {_vectorSize}. Recreating...");
                await _qdrantClient.DeleteCollectionAsync(collectionName);
                await _qdrantClient.CreateCollectionAsync(
                    collectionName,
                    new Qdrant.Client.Grpc.VectorParams { Size = (ulong)_vectorSize, Distance = Qdrant.Client.Grpc.Distance.Cosine },
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [~] Could not check collection dimension: {ex.Message}");
        }
    }
}
