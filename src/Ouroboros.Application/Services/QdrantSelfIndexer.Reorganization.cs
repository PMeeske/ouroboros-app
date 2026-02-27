// <copyright file="QdrantSelfIndexer.Reorganization.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Collections.Concurrent;
using System.Text;
using Ouroboros.Domain;
using Qdrant.Client.Grpc;

/// <summary>
/// Partial class containing self-reorganization logic: access pattern tracking,
/// duplicate removal, clustering, summary creation, and metadata management.
/// </summary>
public sealed partial class QdrantSelfIndexer
{
    #region Self-Reorganization (Learning-Driven Knowledge Optimization)

    // Track access patterns for intelligent reorganization
    private readonly ConcurrentDictionary<string, AccessPattern> _accessPatterns = new();
    private readonly ConcurrentDictionary<string, List<string>> _coAccessLog = new();
    private string? _lastAccessedPointId;

    /// <summary>
    /// Records an access pattern when knowledge is retrieved.
    /// Call this after SearchAsync to enable learning-driven reorganization.
    /// </summary>
    public void RecordAccess(IEnumerable<SearchResult> results)
    {
        var pointIds = results.Select(r => GeneratePointId(r.FilePath, r.ChunkIndex)).ToList();
        var now = DateTime.UtcNow;

        foreach (var result in results)
        {
            var pointId = GeneratePointId(result.FilePath, result.ChunkIndex);

            _accessPatterns.AddOrUpdate(
                pointId,
                _ => new AccessPattern
                {
                    PointId = pointId,
                    FilePath = result.FilePath,
                    AccessCount = 1,
                    LastAccessed = now,
                    CoAccessedWith = pointIds.Where(p => p != pointId).ToList()
                },
                (_, existing) => existing with
                {
                    AccessCount = existing.AccessCount + 1,
                    LastAccessed = now,
                    CoAccessedWith = existing.CoAccessedWith
                        .Concat(pointIds.Where(p => p != pointId))
                        .Distinct()
                        .TakeLast(20)
                        .ToList()
                });

            // Track co-access for clustering
            if (_lastAccessedPointId != null && _lastAccessedPointId != pointId)
            {
                _coAccessLog.AddOrUpdate(
                    pointId,
                    _ => new List<string> { _lastAccessedPointId },
                    (_, list) =>
                    {
                        if (!list.Contains(_lastAccessedPointId)) list.Add(_lastAccessedPointId);
                        return list.TakeLast(50).ToList();
                    });
            }

            _lastAccessedPointId = pointId;
        }
    }

    /// <summary>
    /// Performs intelligent reorganization based on access patterns and content analysis.
    /// Should be called periodically during thinking/learning cycles.
    /// </summary>
    public async Task<ReorganizationResult> ReorganizeAsync(
        bool createSummaries = true,
        bool removeDuplicates = true,
        bool clusterRelated = true,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var insights = new List<string>();
        int clustersFound = 0, consolidated = 0, duplicatesRemoved = 0, summariesCreated = 0;

        await _indexLock.WaitAsync(ct);
        try
        {
            Console.WriteLine("[Reorganize] \ud83e\udde0 Beginning knowledge reorganization...");

            // 1. Find and remove near-duplicates
            if (removeDuplicates)
            {
                var (removed, insight) = await RemoveNearDuplicatesAsync(ct);
                duplicatesRemoved = removed;
                if (!string.IsNullOrEmpty(insight)) insights.Add(insight);
            }

            // 2. Cluster frequently co-accessed content
            if (clusterRelated)
            {
                var (clusters, clusterInsight) = await ClusterRelatedContentAsync(ct);
                clustersFound = clusters;
                if (!string.IsNullOrEmpty(clusterInsight)) insights.Add(clusterInsight);
            }

            // 3. Create summary vectors for high-access clusters
            if (createSummaries)
            {
                var (summaries, summaryInsight) = await CreateClusterSummariesAsync(ct);
                summariesCreated = summaries;
                if (!string.IsNullOrEmpty(summaryInsight)) insights.Add(summaryInsight);
            }

            // 4. Update metadata with access patterns
            consolidated = await UpdateAccessMetadataAsync(ct);

            Console.WriteLine($"[Reorganize] \u2705 Complete: {duplicatesRemoved} duplicates removed, {clustersFound} clusters, {summariesCreated} summaries");

            return new ReorganizationResult
            {
                ClustersFound = clustersFound,
                ConsolidatedChunks = consolidated,
                DuplicatesRemoved = duplicatesRemoved,
                SummariesCreated = summariesCreated,
                Duration = DateTime.UtcNow - startTime,
                Insights = insights
            };
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Lightweight reorganization suitable for calling during active thinking.
    /// Only performs quick optimizations without heavy operations.
    /// </summary>
    public async Task<int> QuickReorganizeAsync(CancellationToken ct = default)
    {
        // Only run if we have enough access patterns to be meaningful
        if (_accessPatterns.Count < 10) return 0;

        int optimizations = 0;

        // Update frequently accessed content with "hot" flag for faster retrieval
        var hotContent = _accessPatterns.Values
            .Where(p => p.AccessCount >= 3)
            .OrderByDescending(p => p.AccessCount)
            .Take(20)
            .ToList();

        foreach (var pattern in hotContent)
        {
            try
            {
                if (Guid.TryParse(pattern.PointId, out var guid))
                {
                    await _client.SetPayloadAsync(
                        _config.CollectionName,
                        new Dictionary<string, Value>
                        {
                            ["access_count"] = pattern.AccessCount,
                            ["last_accessed"] = pattern.LastAccessed.ToString("O"),
                            ["is_hot"] = true
                        },
                        guid,
                        cancellationToken: ct);
                    optimizations++;
                }
            }
            catch (Grpc.Core.RpcException) { /* Point may not exist */ }
        }

        return optimizations;
    }

    private async Task<(int removed, string? insight)> RemoveNearDuplicatesAsync(CancellationToken ct)
    {
        int removed = 0;
        var toRemove = new List<PointId>();

        try
        {
            // Scroll through points and find near-duplicates using vector similarity
            var offset = (PointId?)null;
            var seenContent = new Dictionary<string, (PointId Id, float[] Vector)>();

            while (true)
            {
                var scrollResult = await _client.ScrollAsync(
                    _config.CollectionName,
                    limit: 50,
                    offset: offset,
                    vectorsSelector: new WithVectorsSelector { Enable = true },
                    cancellationToken: ct);

                foreach (var point in scrollResult.Result)
                {
                    var content = point.Payload.TryGetValue("content", out var c) ? c.StringValue : "";
                    var contentHash = Convert.ToBase64String(
                        System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content)).Take(8).ToArray());

                    // Check for exact duplicates (same content hash)
                    if (seenContent.TryGetValue(contentHash, out var existing))
                    {
                        toRemove.Add(point.Id);
                        removed++;
                    }
                    else
                    {
                        // Check for near-duplicates using vector similarity
#pragma warning disable CS0612 // VectorOutput.Data is obsolete
                        var vector = point.Vectors?.Vector?.Data?.ToArray();
#pragma warning restore CS0612
                        if (vector != null)
                        {
                            foreach (var (_, (existingId, existingVector)) in seenContent)
                            {
                                var similarity = CosineSimilarity(vector, existingVector);
                                if (similarity > 0.98f) // Very similar
                                {
                                    toRemove.Add(point.Id);
                                    removed++;
                                    break;
                                }
                            }

                            if (!toRemove.Contains(point.Id))
                            {
                                seenContent[contentHash] = (point.Id, vector);
                            }
                        }
                    }
                }

                if (scrollResult.Result.Count < 50) break;
                offset = scrollResult.Result.Last().Id;
            }

            // Remove duplicates
            if (toRemove.Count > 0)
            {
                await _client.DeleteAsync(_config.CollectionName, toRemove, cancellationToken: ct);
            }
        }
        catch (Grpc.Core.RpcException ex)
        {
            Console.WriteLine($"[Reorganize] Duplicate removal error: {ex.Message}");
        }

        return (removed, removed > 0 ? $"Removed {removed} near-duplicate chunks" : null);
    }

    private async Task<(int clusters, string? insight)> ClusterRelatedContentAsync(CancellationToken ct)
    {
        if (_coAccessLog.Count < 5) return (0, null);

        var clusters = new List<HashSet<string>>();

        // Build clusters from co-access patterns
        foreach (var (pointId, coAccessed) in _coAccessLog)
        {
            var existingCluster = clusters.FirstOrDefault(c => c.Contains(pointId) || coAccessed.Any(ca => c.Contains(ca)));

            if (existingCluster != null)
            {
                existingCluster.Add(pointId);
                foreach (var ca in coAccessed) existingCluster.Add(ca);
            }
            else
            {
                var newCluster = new HashSet<string> { pointId };
                foreach (var ca in coAccessed) newCluster.Add(ca);
                if (newCluster.Count >= 2) clusters.Add(newCluster);
            }
        }

        // Update cluster metadata
        for (int i = 0; i < clusters.Count; i++)
        {
            foreach (var pointId in clusters[i])
            {
                try
                {
                    if (Guid.TryParse(pointId, out var guid))
                    {
                        await _client.SetPayloadAsync(
                            _config.CollectionName,
                            new Dictionary<string, Value>
                            {
                                ["cluster_id"] = i,
                                ["cluster_size"] = clusters[i].Count
                            },
                            guid,
                            cancellationToken: ct);
                    }
                }
                catch (Grpc.Core.RpcException) { /* Point may not exist */ }
            }
        }

        return (clusters.Count, clusters.Count > 0 ? $"Identified {clusters.Count} knowledge clusters from access patterns" : null);
    }

    private async Task<(int summaries, string? insight)> CreateClusterSummariesAsync(CancellationToken ct)
    {
        // Find high-access clusters and create summary vectors
        var hotClusters = _accessPatterns.Values
            .Where(p => p.AccessCount >= 5)
            .GroupBy(p => p.FilePath)
            .Where(g => g.Count() >= 2)
            .Take(5)
            .ToList();

        int summariesCreated = 0;

        foreach (var cluster in hotClusters)
        {
            try
            {
                // Aggregate content from cluster
                var contents = new List<string>();
                foreach (var pattern in cluster.Take(5))
                {
                    var results = await _client.RetrieveAsync(
                        _config.CollectionName,
                        new[] { new PointId { Uuid = pattern.PointId } },
                        withPayload: true,
                        cancellationToken: ct);

                    foreach (var point in results)
                    {
                        if (point.Payload.TryGetValue("content", out var c))
                        {
                            contents.Add(c.StringValue);
                        }
                    }
                }

                if (contents.Count < 2) continue;

                // Create a summary embedding from combined content
                var summaryText = $"[SUMMARY] Key concepts from {cluster.Key}: " +
                    string.Join(" | ", contents.Select(c => c.Length > 100 ? c.Substring(0, 100) : c));

                var summaryEmbedding = await _embedding.CreateEmbeddingsAsync(summaryText, ct);
                var summaryId = Guid.NewGuid().ToString();

                await _client.UpsertAsync(
                    _config.CollectionName,
                    new[]
                    {
                        new PointStruct
                        {
                            Id = new PointId { Uuid = summaryId },
                            Vectors = summaryEmbedding,
                            Payload =
                            {
                                ["file_path"] = cluster.Key,
                                ["content"] = summaryText,
                                ["is_summary"] = true,
                                ["summarizes_count"] = contents.Count,
                                ["created_at"] = DateTime.UtcNow.ToString("O")
                            }
                        }
                    },
                    cancellationToken: ct);

                summariesCreated++;
            }
            catch (Grpc.Core.RpcException ex)
            {
                Console.WriteLine($"[Reorganize] Summary creation error: {ex.Message}");
            }
        }

        return (summariesCreated, summariesCreated > 0 ? $"Created {summariesCreated} summary vectors for frequently accessed content" : null);
    }

    private async Task<int> UpdateAccessMetadataAsync(CancellationToken ct)
    {
        int updated = 0;

        foreach (var pattern in _accessPatterns.Values.Take(100))
        {
            try
            {
                if (Guid.TryParse(pattern.PointId, out var guid))
                {
                    await _client.SetPayloadAsync(
                        _config.CollectionName,
                        new Dictionary<string, Value>
                        {
                            ["access_count"] = pattern.AccessCount,
                            ["last_accessed"] = pattern.LastAccessed.ToString("O")
                        },
                        guid,
                        cancellationToken: ct);
                    updated++;
                }
            }
            catch (Grpc.Core.RpcException) { /* Point may not exist */ }
        }

        return updated;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(magA) * Math.Sqrt(magB);
        return magnitude > 0 ? (float)(dot / magnitude) : 0;
    }

    /// <summary>
    /// Gets reorganization statistics and insights.
    /// </summary>
    public ReorganizationStats GetReorganizationStats()
    {
        var hotContent = _accessPatterns.Values.Where(p => p.AccessCount >= 3).ToList();
        var clusters = _coAccessLog.Count;

        return new ReorganizationStats
        {
            TrackedPatterns = _accessPatterns.Count,
            HotContentCount = hotContent.Count,
            CoAccessClusters = clusters,
            TopAccessedFiles = _accessPatterns.Values
                .GroupBy(p => p.FilePath)
                .OrderByDescending(g => g.Sum(p => p.AccessCount))
                .Take(5)
                .Select(g => (g.Key, g.Sum(p => p.AccessCount)))
                .ToList()
        };
    }

    #endregion
}
