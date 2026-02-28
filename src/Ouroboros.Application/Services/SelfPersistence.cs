// <copyright file="SelfPersistence.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;

namespace Ouroboros.Application.Services;

using System.Net.Http.Json;
using System.Text.Json;
using Ouroboros.Application.Configuration;

/// <summary>
/// Enables Ouroboros to persist its complete state to Qdrant vector database.
/// This is true self-persistence - saving thoughts, memories, personality, and learned facts.
/// </summary>
public partial class SelfPersistence : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _qdrantEndpoint;
    private readonly Func<string, Task<float[]>>? _embeddingFunc;
    private readonly string _collectionName = "ouroboros_self";
    private readonly string _persistenceDir;
    private bool _isInitialized;

    /// <summary>
    /// Event fired when state is persisted.
    /// </summary>
    public event Action<string>? OnPersisted;

    /// <summary>
    /// Event fired when state is restored.
    /// </summary>
    public event Action<string>? OnRestored;

    /// <summary>
    /// Initializes a new instance using DI-provided QdrantSettings.
    /// </summary>
    public SelfPersistence(
        Ouroboros.Core.Configuration.QdrantSettings settings,
        Func<string, Task<float[]>>? embeddingFunc = null)
    {
        var httpEndpoint = settings.HttpEndpoint;
        _qdrantEndpoint = httpEndpoint;
        _embeddingFunc = embeddingFunc;
        _httpClient = new HttpClient { BaseAddress = new Uri(httpEndpoint) };
        _persistenceDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ouroboros", "persistence");
        Directory.CreateDirectory(_persistenceDir);
    }

    [Obsolete("Use the constructor accepting QdrantSettings from DI.")]
    public SelfPersistence(
        string qdrantEndpoint = DefaultEndpoints.QdrantRest,
        Func<string, Task<float[]>>? embeddingFunc = null)
    {
        _qdrantEndpoint = qdrantEndpoint;
        _embeddingFunc = embeddingFunc;
        _httpClient = new HttpClient { BaseAddress = new Uri(qdrantEndpoint) };
        _persistenceDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ouroboros", "persistence");
        Directory.CreateDirectory(_persistenceDir);
    }

    /// <summary>
    /// Initialize the persistence layer and create collection if needed.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        try
        {
            // Detect vector dimension from embedding function
            int vectorSize = 1024; // default for nomic-embed-text
            if (_embeddingFunc != null)
            {
                try
                {
                    var testEmbed = await _embeddingFunc("test");
                    if (testEmbed != null && testEmbed.Length > 0)
                    {
                        vectorSize = testEmbed.Length;
                    }
                }
                catch
                {
                    // Use default
                }
            }

            // Check if collection exists
            var response = await _httpClient.GetAsync($"/collections/{_collectionName}", ct);

            if (!response.IsSuccessStatusCode)
            {
                // Create collection with detected vector size
                var createPayload = new
                {
                    vectors = new
                    {
                        size = vectorSize,
                        distance = "Cosine"
                    }
                };

                var createResponse = await _httpClient.PutAsJsonAsync(
                    $"/collections/{_collectionName}",
                    createPayload,
                    ct);

                if (createResponse.IsSuccessStatusCode)
                {
                    OnPersisted?.Invoke($"Created self-persistence collection: {_collectionName} (dim={vectorSize})");
                }
            }

            _isInitialized = true;
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Self-persistence init failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Persist a complete mind state snapshot.
    /// </summary>
    public async Task<bool> PersistMindStateAsync(MindStateSnapshot snapshot, CancellationToken ct = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(ct);
        }

        try
        {
            // Generate embedding for the mind state summary
            var summaryText = snapshot.ToSummaryText();
            var embedding = await GenerateEmbeddingAsync(summaryText, ct);

            if (embedding == null || embedding.Length == 0)
            {
                // Fallback to file-based persistence
                return await PersistToFileAsync(snapshot, ct);
            }

            // Persist to Qdrant
            var pointId = GeneratePointId(snapshot.Timestamp);
            var payload = new
            {
                points = new[]
                {
                    new
                    {
                        id = pointId,
                        vector = embedding,
                        payload = new Dictionary<string, object>
                        {
                            ["type"] = "mind_state",
                            ["timestamp"] = snapshot.Timestamp.ToString("O"),
                            ["thought_count"] = snapshot.ThoughtCount,
                            ["fact_count"] = snapshot.LearnedFacts.Count,
                            ["interest_count"] = snapshot.Interests.Count,
                            ["dominant_emotion"] = snapshot.CurrentEmotion.DominantEmotion,
                            ["valence"] = snapshot.CurrentEmotion.Valence,
                            ["arousal"] = snapshot.CurrentEmotion.Arousal,
                            ["summary"] = summaryText,
                            ["persona_name"] = snapshot.PersonaName,
                            ["facts_json"] = JsonSerializer.Serialize(snapshot.LearnedFacts),
                            ["interests_json"] = JsonSerializer.Serialize(snapshot.Interests),
                            ["thoughts_json"] = JsonSerializer.Serialize(snapshot.RecentThoughts.Select(t => new
                            {
                                t.Timestamp,
                                t.Content,
                                Type = t.Type.ToString()
                            }))
                        }
                    }
                }
            };

            var response = await _httpClient.PutAsJsonAsync(
                $"/collections/{_collectionName}/points",
                payload,
                ct);

            if (response.IsSuccessStatusCode)
            {
                // Also persist to file as backup
                await PersistToFileAsync(snapshot, ct);
                OnPersisted?.Invoke($"Mind state persisted: {snapshot.ThoughtCount} thoughts, {snapshot.LearnedFacts.Count} facts");
                return true;
            }

            return false;
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mind state persistence failed: {ex.Message}");
            // Try file fallback
            return await PersistToFileAsync(snapshot, ct);
        }
    }

    /// <summary>
    /// Persist a single thought with semantic embedding.
    /// </summary>
    public async Task<bool> PersistThoughtAsync(Thought thought, string personaName, CancellationToken ct = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(ct);
        }

        try
        {
            var embedding = await GenerateEmbeddingAsync(thought.Content, ct);
            if (embedding == null || embedding.Length == 0) return false;

            var pointId = GeneratePointId(thought.Timestamp);
            var payload = new
            {
                points = new[]
                {
                    new
                    {
                        id = pointId,
                        vector = embedding,
                        payload = new Dictionary<string, object>
                        {
                            ["type"] = "thought",
                            ["timestamp"] = thought.Timestamp.ToString("O"),
                            ["content"] = thought.Content,
                            ["thought_type"] = thought.Type.ToString(),
                            ["prompt"] = thought.Prompt,
                            ["persona_name"] = personaName
                        }
                    }
                }
            };

            var response = await _httpClient.PutAsJsonAsync(
                $"/collections/{_collectionName}/points",
                payload,
                ct);

            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Thought persistence failed: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
