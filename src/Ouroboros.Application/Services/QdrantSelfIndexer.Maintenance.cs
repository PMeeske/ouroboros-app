// <copyright file="QdrantSelfIndexer.Maintenance.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Security.Cryptography;
using System.Text;
using Qdrant.Client.Grpc;

/// <summary>
/// Partial class containing collection health management, file indexing internals,
/// file discovery, hashing, statistics, and progress reporting.
/// </summary>
public sealed partial class QdrantSelfIndexer
{
    private async Task<int> IndexFileAsync(string filePath, bool forceReindex, CancellationToken ct)
    {
        if (!ShouldIndexFile(filePath)) return 0;

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists || fileInfo.Length > _config.MaxFileSize) return 0;

        var currentHash = ComputeFileHash(filePath);

        if (!forceReindex && _fileHashes.TryGetValue(filePath, out var storedHash) && storedHash == currentHash)
        {
            return 0; // File unchanged
        }

        var content = await File.ReadAllTextAsync(filePath, ct);
        if (string.IsNullOrWhiteSpace(content)) return 0;

        var chunks = _splitter.SplitText(content);
        if (chunks.Count == 0) return 0;

        var points = new List<PointStruct>();
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            if (string.IsNullOrWhiteSpace(chunk)) continue;

            var embedding = await _embedding.CreateEmbeddingsAsync(chunk, ct);
            var pointId = GeneratePointId(filePath, i);

            points.Add(new PointStruct
            {
                Id = new PointId { Uuid = pointId },
                Vectors = embedding,
                Payload =
                {
                    ["file_path"] = filePath,
                    ["file_name"] = Path.GetFileName(filePath),
                    ["extension"] = Path.GetExtension(filePath),
                    ["chunk_index"] = i,
                    ["chunk_count"] = chunks.Count,
                    ["content"] = chunk,
                    ["indexed_at"] = DateTime.UtcNow.ToString("O"),
                    ["file_hash"] = currentHash
                }
            });
        }

        if (points.Count > 0)
        {
            // Upsert in batches
            for (int i = 0; i < points.Count; i += _config.BatchSize)
            {
                var batch = points.Skip(i).Take(_config.BatchSize).ToList();
                await _client.UpsertAsync(_config.CollectionName, batch, cancellationToken: ct);
            }

            // Store file hash
            await StoreFileHashAsync(filePath, currentHash, ct);
            _fileHashes[filePath] = currentHash;
        }

        return points.Count;
    }

    private async Task RemoveFileFromIndexAsync(string filePath, CancellationToken ct)
    {
        // Delete all points for this file
        await _client.DeleteAsync(
            _config.CollectionName,
            new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "file_path",
                            Match = new Match { Keyword = filePath }
                        }
                    }
                }
            },
            cancellationToken: ct);

        // Remove hash
        await _client.DeleteAsync(
            _config.HashCollectionName,
            new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "file_path",
                            Match = new Match { Keyword = filePath }
                        }
                    }
                }
            },
            cancellationToken: ct);
    }

    private List<string> DiscoverFiles()
    {
        var files = new List<string>();
        foreach (var root in _config.RootPaths)
        {
            if (!Directory.Exists(root)) continue;

            try
            {
                files.AddRange(
                    Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                        .Where(ShouldIndexFile));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DiscoverError] {root}: {ex.Message}");
            }
        }
        return files;
    }

    private bool ShouldIndexFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

        // Check excluded directories
        var parts = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(p => _config.ExcludeDirectories.Contains(p)))
        {
            return false;
        }

        // Check extension
        if (!_config.Extensions.Contains(extension))
        {
            return false;
        }

        // Skip hidden files
        if (fileName.StartsWith('.'))
        {
            return false;
        }

        return true;
    }

    private static string ComputeFileHash(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            return Convert.ToBase64String(hash);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GeneratePointId(string filePath, int chunkIndex)
    {
        using var sha256 = SHA256.Create();
        var input = $"{filePath}::{chunkIndex}";
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return new Guid(hash.Take(16).ToArray()).ToString();
    }

    private async Task EnsureCollectionExistsAsync(string collectionName, CancellationToken ct, bool isHashCollection = false)
    {
        var expectedSize = isHashCollection ? 4u : (uint)_config.VectorSize;
        var exists = await _client.CollectionExistsAsync(collectionName, ct);

        if (exists)
        {
            // Validate dimensions match - if not, recreate the collection
            try
            {
                var info = await _client.GetCollectionInfoAsync(collectionName);
                var currentSize = info.Config?.Params?.VectorsConfig?.Params?.Size ?? 0;

                if (currentSize != expectedSize)
                {
                    Console.WriteLine($"[CollectionDimensionMismatch] {collectionName}: expected {expectedSize}D, got {currentSize}D. Recreating...");
                    await _client.DeleteCollectionAsync(collectionName);
                    exists = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CollectionInfoError] {collectionName}: {ex.Message}");
            }
        }

        if (!exists)
        {
            await _client.CreateCollectionAsync(
                collectionName,
                new VectorParams
                {
                    Size = expectedSize,
                    Distance = Distance.Cosine
                },
                cancellationToken: ct);
        }
    }

    private async Task ClearCollectionAsync(string collectionName, CancellationToken ct)
    {
        var exists = await _client.CollectionExistsAsync(collectionName, ct);
        if (exists)
        {
            await _client.DeleteCollectionAsync(collectionName);
            await EnsureCollectionExistsAsync(collectionName, ct, collectionName == _config.HashCollectionName);
        }
    }

    private async Task LoadFileHashesAsync(CancellationToken ct)
    {
        try
        {
            var exists = await _client.CollectionExistsAsync(_config.HashCollectionName, ct);
            if (!exists) return;

            // Scroll through all hash records
            var offset = (PointId?)null;
            while (true)
            {
                var scrollResult = await _client.ScrollAsync(
                    _config.HashCollectionName,
                    limit: 100,
                    offset: offset,
                    cancellationToken: ct);

                foreach (var point in scrollResult.Result)
                {
                    if (point.Payload.TryGetValue("file_path", out var fp) &&
                        point.Payload.TryGetValue("file_hash", out var fh))
                    {
                        _fileHashes[fp.StringValue] = fh.StringValue;
                    }
                }

                if (scrollResult.Result.Count < 100) break;
                offset = scrollResult.Result.Last().Id;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadHashesError] {ex.Message}");
        }
    }

    private async Task StoreFileHashAsync(string filePath, string hash, CancellationToken ct)
    {
        var pointId = GeneratePointId(filePath, -1); // -1 for hash entry
        var point = new PointStruct
        {
            Id = new PointId { Uuid = pointId },
            Vectors = new float[] { 0, 0, 0, 0 }, // Dummy vector for hash storage
            Payload =
            {
                ["file_path"] = filePath,
                ["file_hash"] = hash,
                ["indexed_at"] = DateTime.UtcNow.ToString("O")
            }
        };

        try
        {
            await _client.UpsertAsync(_config.HashCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Grpc.Core.RpcException ex) when (ex.Message.Contains("dimension error"))
        {
            // Collection has wrong dimensions - recreate it
            Console.WriteLine($"[HashCollectionFix] Dimension mismatch detected, recreating {_config.HashCollectionName}...");
            try
            {
                await _client.DeleteCollectionAsync(_config.HashCollectionName);
                await _client.CreateCollectionAsync(
                    _config.HashCollectionName,
                    new VectorParams { Size = 4, Distance = Distance.Cosine },
                    cancellationToken: ct);
                // Retry the upsert
                await _client.UpsertAsync(_config.HashCollectionName, new[] { point }, cancellationToken: ct);
                Console.WriteLine($"[HashCollectionFix] Successfully recreated and stored hash.");
            }
            catch (Exception innerEx)
            {
                Console.WriteLine($"[HashCollectionFixError] {innerEx.Message}");
            }
        }
    }

    private void ReportProgress(IProgress<IndexingProgress>? progress, IndexingStats stats, string currentFile, DateTime startTime)
    {
        var p = CreateProgress(stats, startTime, isComplete: false, currentFile: currentFile);
        progress?.Report(p);
        OnProgress?.Invoke(p);
    }

    private static IndexingProgress CreateProgress(IndexingStats stats, DateTime startTime, bool isComplete, string? currentFile = null)
    {
        return new IndexingProgress
        {
            TotalFiles = stats.TotalFiles,
            ProcessedFiles = stats.ProcessedFiles,
            IndexedChunks = stats.IndexedChunks,
            SkippedFiles = stats.SkippedFiles,
            ErrorFiles = stats.ErrorFiles,
            CurrentFile = currentFile ?? stats.CurrentFile,
            Elapsed = DateTime.UtcNow - startTime,
            IsComplete = isComplete
        };
    }

    private class IndexingStats
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int IndexedChunks { get; set; }
        public int SkippedFiles { get; set; }
        public int ErrorFiles { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
    }
}
