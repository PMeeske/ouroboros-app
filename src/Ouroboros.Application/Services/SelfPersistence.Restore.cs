// <copyright file="SelfPersistence.Restore.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Json;
using System.Text.Json;

namespace Ouroboros.Application.Services;

/// <summary>
/// Restore and search operations for self-persistence.
/// </summary>
public partial class SelfPersistence
{
    /// <summary>
    /// Persist a learned fact with semantic embedding.
    /// </summary>
    public async Task<bool> PersistLearnedFactAsync(string fact, string sourceQuery, string personaName, CancellationToken ct = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(ct);
        }

        try
        {
            var embedding = await GenerateEmbeddingAsync(fact, ct);
            if (embedding == null || embedding.Length == 0) return false;

            var pointId = GeneratePointId(DateTime.UtcNow);
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
                            ["type"] = "learned_fact",
                            ["timestamp"] = DateTime.UtcNow.ToString("O"),
                            ["content"] = fact,
                            ["source_query"] = sourceQuery,
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
            System.Diagnostics.Debug.WriteLine($"Fact persistence failed: {ex.Message}");
            return false;
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fact persistence failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restore mind state from Qdrant.
    /// </summary>
    public async Task<MindStateSnapshot?> RestoreLatestMindStateAsync(string personaName, CancellationToken ct = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(ct);
        }

        try
        {
            // First try file-based restore (most reliable)
            var fileSnapshot = await RestoreFromFileAsync(personaName, ct);
            if (fileSnapshot != null)
            {
                OnRestored?.Invoke($"Mind state restored from file: {fileSnapshot.ThoughtCount} thoughts");
                return fileSnapshot;
            }

            // Try Qdrant scroll to find latest mind state
            var scrollPayload = new
            {
                filter = new
                {
                    must = new[]
                    {
                        new { key = "type", match = new { value = "mind_state" } },
                        new { key = "persona_name", match = new { value = personaName } }
                    }
                },
                limit = 1,
                with_payload = true,
                order_by = new { key = "timestamp", direction = "desc" }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/collections/{_collectionName}/points/scroll",
                scrollPayload,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<QdrantScrollResponse>(ct);
                if (result?.Result?.Points?.Length > 0)
                {
                    var point = result.Result.Points[0];
                    var snapshot = ParseMindStateFromPayload(point.Payload);
                    if (snapshot != null)
                    {
                        OnRestored?.Invoke($"Mind state restored from Qdrant: {snapshot.ThoughtCount} thoughts");
                        return snapshot;
                    }
                }
            }

            return null;
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mind state restore failed: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mind state restore failed: {ex.Message}");
            return null;
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mind state restore failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Search for related thoughts by semantic similarity.
    /// </summary>
    public async Task<List<Thought>> SearchRelatedThoughtsAsync(string query, int limit = 5, CancellationToken ct = default)
    {
        var results = new List<Thought>();

        try
        {
            var embedding = await GenerateEmbeddingAsync(query, ct);
            if (embedding == null || embedding.Length == 0) return results;

            var searchPayload = new
            {
                vector = embedding,
                filter = new
                {
                    must = new[]
                    {
                        new { key = "type", match = new { value = "thought" } }
                    }
                },
                limit = limit,
                with_payload = true
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/collections/{_collectionName}/points/search",
                searchPayload,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(ct);
                if (result?.Result != null)
                {
                    foreach (var hit in result.Result)
                    {
                        if (hit.Payload != null)
                        {
                            results.Add(new Thought
                            {
                                Timestamp = DateTime.Parse(hit.Payload.GetValueOrDefault("timestamp")?.ToString() ?? DateTime.UtcNow.ToString("O")),
                                Content = hit.Payload.GetValueOrDefault("content")?.ToString() ?? "",
                                Prompt = hit.Payload.GetValueOrDefault("prompt")?.ToString() ?? "",
                                Type = Enum.Parse<ThoughtType>(hit.Payload.GetValueOrDefault("thought_type")?.ToString() ?? "Reflection")
                            });
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Thought search failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Thought search failed: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Search for related learned facts.
    /// </summary>
    public async Task<List<string>> SearchRelatedFactsAsync(string query, int limit = 5, CancellationToken ct = default)
    {
        var results = new List<string>();

        try
        {
            var embedding = await GenerateEmbeddingAsync(query, ct);
            if (embedding == null || embedding.Length == 0) return results;

            var searchPayload = new
            {
                vector = embedding,
                filter = new
                {
                    must = new[]
                    {
                        new { key = "type", match = new { value = "learned_fact" } }
                    }
                },
                limit = limit,
                with_payload = true
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/collections/{_collectionName}/points/search",
                searchPayload,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(ct);
                if (result?.Result != null)
                {
                    foreach (var hit in result.Result)
                    {
                        var content = hit.Payload?.GetValueOrDefault("content")?.ToString();
                        if (!string.IsNullOrEmpty(content))
                        {
                            results.Add(content);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fact search failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fact search failed: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Get persistence statistics.
    /// </summary>
    public async Task<PersistenceStats> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = new PersistenceStats();

        try
        {
            var response = await _httpClient.GetAsync($"/collections/{_collectionName}", ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (result.TryGetProperty("result", out var resultProp))
                {
                    if (resultProp.TryGetProperty("points_count", out var pointsCount))
                    {
                        stats.TotalPoints = pointsCount.GetInt64();
                    }
                }

                stats.IsConnected = true;
                stats.CollectionName = _collectionName;
            }

            // Count file backups
            stats.FileBackups = Directory.GetFiles(_persistenceDir, "*.json").Length;
        }
        catch
        {
            stats.IsConnected = false;
        }

        return stats;
    }
}
