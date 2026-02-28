// <copyright file="IntelligentToolLearner.PatternStorage.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Text.Json;
using Ouroboros.Tools;
using Qdrant.Client;
using Qdrant.Client.Grpc;

/// <summary>
/// Qdrant persistence, pattern matching, and storage for IntelligentToolLearner.
/// </summary>
public sealed partial class IntelligentToolLearner
{
    /// <summary>
    /// Minimum similarity score threshold for pattern matching.
    /// Patterns with similarity below this value are considered unrelated.
    /// </summary>
    private const float MinimumPatternSimilarityThreshold = 0.75f;

    /// <summary>
    /// Finds a matching learned pattern in Qdrant using semantic search.
    /// </summary>
    private async Task<LearnedToolPattern?> FindMatchingPatternAsync(string goal, CancellationToken ct)
    {
        try
        {
            // First check cache - require exact goal match or high substring overlap
            var cachedMatch = _patternCache.Values
                .Where(p => p.Goal.Equals(goal, StringComparison.OrdinalIgnoreCase) ||
                           (p.RelatedGoals.Any(g => g.Equals(goal, StringComparison.OrdinalIgnoreCase)) ||
                            p.RelatedGoals.Any(g => ComputeOverlapRatio(g, goal) >= 0.6)))
                .OrderByDescending(p => p.SuccessRate)
                .FirstOrDefault();

            if (cachedMatch != null)
                return cachedMatch;

            // Semantic search in Qdrant
            var embedding = await _embedding.CreateEmbeddingsAsync(goal);

            var collectionExists = await _qdrantClient.CollectionExistsAsync(_collectionName, ct);
            if (!collectionExists) return null;

            var searchResults = await _qdrantClient.SearchAsync(
                _collectionName,
                embedding,
                limit: 3,
                cancellationToken: ct);

            foreach (var result in searchResults)
            {
                if (result.Score < MinimumPatternSimilarityThreshold) continue; // Threshold for similarity

                if (result.Payload.TryGetValue("pattern_json", out var jsonValue))
                {
                    var pattern = JsonSerializer.Deserialize<LearnedToolPattern>(jsonValue.StringValue);
                    if (pattern != null)
                    {
                        _patternCache[pattern.Id] = pattern;
                        return pattern;
                    }
                }
            }

            return null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            Console.Error.WriteLine($"[WARN] Pattern search failed: {ex.Message}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"[WARN] Pattern search failed: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"[WARN] Pattern search failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Persists a learned tool pattern to Qdrant.
    /// </summary>
    private async Task PersistPatternAsync(
        string goal,
        string toolName,
        ToolConfiguration config,
        CancellationToken ct)
    {
        try
        {
            var pattern = new LearnedToolPattern(
                Id: Guid.NewGuid().ToString("N"),
                Goal: goal,
                ToolName: toolName,
                Configuration: config,
                SuccessRate: 1.0,
                UsageCount: 1,
                CreatedAt: DateTime.UtcNow,
                LastUsed: DateTime.UtcNow,
                RelatedGoals: new List<string>());

            // Add to cache
            _patternCache[pattern.Id] = pattern;

            // Create embedding for the goal
            var embedding = await _embedding.CreateEmbeddingsAsync(goal);

            // Store in Qdrant
            var point = new PointStruct
            {
                Id = new PointId { Uuid = pattern.Id },
                Vectors = embedding,
                Payload =
                {
                    ["goal"] = goal,
                    ["tool_name"] = toolName,
                    ["pattern_json"] = JsonSerializer.Serialize(pattern),
                    ["created_at"] = pattern.CreatedAt.ToString("O")
                }
            };

            await _qdrantClient.UpsertAsync(_collectionName, new[] { point }, cancellationToken: ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to persist pattern: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to persist pattern: {ex.Message}");
        }
    }

    /// <summary>
    /// Records usage of a pattern for learning optimization.
    /// </summary>
    private async Task RecordPatternUsageAsync(string patternId, bool success, CancellationToken ct)
    {
        try
        {
            if (_patternCache.TryGetValue(patternId, out var pattern))
            {
                int newCount = pattern.UsageCount + 1;
                double newSuccessRate = ((pattern.SuccessRate * pattern.UsageCount) + (success ? 1.0 : 0.0)) / newCount;

                var updatedPattern = pattern with
                {
                    UsageCount = newCount,
                    SuccessRate = newSuccessRate,
                    LastUsed = DateTime.UtcNow
                };

                _patternCache[patternId] = updatedPattern;

                // Update in Qdrant
                var embedding = await _embedding.CreateEmbeddingsAsync(pattern.Goal);
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = patternId },
                    Vectors = embedding,
                    Payload =
                    {
                        ["goal"] = pattern.Goal,
                        ["tool_name"] = pattern.ToolName,
                        ["pattern_json"] = JsonSerializer.Serialize(updatedPattern),
                        ["created_at"] = pattern.CreatedAt.ToString("O"),
                        ["success_rate"] = newSuccessRate
                    }
                };

                await _qdrantClient.UpsertAsync(_collectionName, new[] { point }, cancellationToken: ct);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to record pattern usage: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to record pattern usage: {ex.Message}");
        }
    }

    /// <summary>
    /// Recreates a tool from a stored pattern.
    /// </summary>
    private async Task<Result<ITool, string>> RecreateToolFromPatternAsync(
        LearnedToolPattern pattern,
        ToolRegistry registry,
        CancellationToken ct)
    {
        var result = await _toolFactory.CreateToolAsync(
            pattern.ToolName,
            pattern.Configuration.Description,
            ct);

        // Note: Caller is responsible for updating registry since it's immutable

        return result;
    }

    /// <summary>
    /// Detects vector dimension from embedding model.
    /// </summary>
    private async Task DetectVectorSizeAsync(CancellationToken ct)
    {
        try
        {
            var testEmbedding = await _embedding.CreateEmbeddingsAsync("test", ct);
            if (testEmbedding.Length > 0)
            {
                _vectorSize = testEmbedding.Length;
            }
        }
        catch
        {
            // Keep default vector size
        }
    }

    /// <summary>
    /// Ensures the Qdrant collection exists with correct dimensions.
    /// </summary>
    private async Task EnsureCollectionExistsAsync(CancellationToken ct)
    {
        try
        {
            var exists = await _qdrantClient.CollectionExistsAsync(_collectionName, ct);
            if (exists)
            {
                // Check if dimension matches - use same pattern as PersonalityEngine
                var info = await _qdrantClient.GetCollectionInfoAsync(_collectionName, ct);
                var existingSize = info.Config?.Params?.VectorsConfig?.Params?.Size ?? 0;
                if (existingSize > 0 && existingSize != (ulong)_vectorSize)
                {
                    Console.WriteLine($"  [!] Collection {_collectionName} has dimension {existingSize}, expected {_vectorSize}. Recreating...");
                    await _qdrantClient.DeleteCollectionAsync(_collectionName);
                    exists = false;
                }
            }

            if (!exists)
            {
                await _qdrantClient.CreateCollectionAsync(
                    _collectionName,
                    new VectorParams
                    {
                        Size = (ulong)_vectorSize,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: ct);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            Console.Error.WriteLine($"[WARN] Qdrant collection setup failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"[WARN] Qdrant collection setup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads existing patterns from Qdrant into cache.
    /// </summary>
    private async Task LoadPatternsFromQdrantAsync(CancellationToken ct)
    {
        try
        {
            var exists = await _qdrantClient.CollectionExistsAsync(_collectionName, ct);
            if (!exists) return;

            var points = await _qdrantClient.ScrollAsync(
                _collectionName,
                limit: 100,
                cancellationToken: ct);

            foreach (var point in points.Result)
            {
                if (point.Payload.TryGetValue("pattern_json", out var jsonValue))
                {
                    var pattern = JsonSerializer.Deserialize<LearnedToolPattern>(jsonValue.StringValue);
                    if (pattern != null)
                    {
                        _patternCache[pattern.Id] = pattern;
                    }
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to load patterns from Qdrant: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to load patterns from Qdrant: {ex.Message}");
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to load patterns from Qdrant: {ex.Message}");
        }
    }
}
