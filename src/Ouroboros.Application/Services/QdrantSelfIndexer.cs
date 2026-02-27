// <copyright file="QdrantSelfIndexer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using LangChain.Splitters.Text;
using Ouroboros.Core.Configuration;
using Ouroboros.Domain;
using Qdrant.Client;
using Qdrant.Client.Grpc;

/// <summary>
/// Qdrant-based self-indexer for Ouroboros workspace content.
/// Supports full reindex, incremental updates, and live file watching.
/// </summary>
public sealed partial class QdrantSelfIndexer : IAsyncDisposable
{
    private readonly QdrantClient _client;
    private readonly IEmbeddingModel _embedding;
    private readonly QdrantIndexerConfig _config;
    private readonly RecursiveCharacterTextSplitter _splitter;
    private readonly ConcurrentDictionary<string, string> _fileHashes = new();
    private readonly ConcurrentDictionary<string, DateTime> _pendingChanges = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly CancellationTokenSource _watcherCts = new();
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private Task? _debounceTask;
    private bool _isInitialized;
    private bool _isDisposed;

    /// <summary>
    /// Event raised when indexing progress updates.
    /// </summary>
    public event Action<IndexingProgress>? OnProgress;

    /// <summary>
    /// Event raised when a file is indexed.
    /// </summary>
    public event Action<string, int>? OnFileIndexed;

    /// <summary>
    /// Initializes a new instance using the DI-provided client and collection registry.
    /// </summary>
    public QdrantSelfIndexer(
        QdrantClient client,
        IQdrantCollectionRegistry registry,
        IEmbeddingModel embedding,
        QdrantIndexerConfig? config = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentNullException.ThrowIfNull(registry);
        _embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
        _config = config ?? new QdrantIndexerConfig();
        _config = _config with
        {
            CollectionName = registry.GetCollectionName(QdrantCollectionRole.SelfIndex),
            HashCollectionName = registry.GetCollectionName(QdrantCollectionRole.FileHashes),
        };

        _splitter = new RecursiveCharacterTextSplitter(
            chunkSize: _config.ChunkSize,
            chunkOverlap: _config.ChunkOverlap);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantSelfIndexer"/> class.
    /// </summary>
    [Obsolete("Use the constructor accepting QdrantClient + IQdrantCollectionRegistry from DI.")]
    public QdrantSelfIndexer(IEmbeddingModel embedding, QdrantIndexerConfig? config = null)
    {
        _embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
        _config = config ?? new QdrantIndexerConfig();

        var uri = new Uri(_config.QdrantEndpoint);
        _client = new QdrantClient(uri.Host, uri.Port > 0 ? uri.Port : 6334, uri.Scheme == "https");

        _splitter = new RecursiveCharacterTextSplitter(
            chunkSize: _config.ChunkSize,
            chunkOverlap: _config.ChunkOverlap);
    }

    /// <summary>
    /// Initializes the indexer, creating collections if needed.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        await EnsureCollectionExistsAsync(_config.CollectionName, ct);
        await EnsureCollectionExistsAsync(_config.HashCollectionName, ct, isHashCollection: true);
        await LoadFileHashesAsync(ct);

        if (_config.EnableFileWatcher)
        {
            StartFileWatchers();
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Performs a full reindex of all configured paths.
    /// </summary>
    public async Task<IndexingProgress> FullReindexAsync(
        bool clearExisting = true,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken ct = default)
    {
        await _indexLock.WaitAsync(ct);
        try
        {
            var startTime = DateTime.UtcNow;
            var stats = new IndexingStats();

            if (clearExisting)
            {
                await ClearCollectionAsync(_config.CollectionName, ct);
                await ClearCollectionAsync(_config.HashCollectionName, ct);
                _fileHashes.Clear();
            }

            var allFiles = DiscoverFiles();
            stats.TotalFiles = allFiles.Count;

            ReportProgress(progress, stats, string.Empty, startTime);

            foreach (var file in allFiles)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    stats.CurrentFile = file;
                    var chunks = await IndexFileAsync(file, forceReindex: true, ct);
                    if (chunks > 0)
                    {
                        stats.ProcessedFiles++;
                        stats.IndexedChunks += chunks;
                        OnFileIndexed?.Invoke(file, chunks);
                    }
                    else
                    {
                        stats.SkippedFiles++;
                    }
                }
                catch (Exception ex)
                {
                    stats.ErrorFiles++;
                    Console.WriteLine($"[IndexError] {file}: {ex.Message}");
                }

                ReportProgress(progress, stats, file, startTime);
            }

            var finalProgress = CreateProgress(stats, startTime, isComplete: true);
            OnProgress?.Invoke(finalProgress);
            return finalProgress;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Performs incremental indexing, only updating changed files.
    /// </summary>
    public async Task<IndexingProgress> IncrementalIndexAsync(
        IProgress<IndexingProgress>? progress = null,
        CancellationToken ct = default)
    {
        await _indexLock.WaitAsync(ct);
        try
        {
            var startTime = DateTime.UtcNow;
            var stats = new IndexingStats();

            var allFiles = DiscoverFiles();
            var changedFiles = new List<string>();

            // Identify changed files
            foreach (var file in allFiles)
            {
                var currentHash = ComputeFileHash(file);
                if (!_fileHashes.TryGetValue(file, out var storedHash) || storedHash != currentHash)
                {
                    changedFiles.Add(file);
                }
            }

            // Identify deleted files
            var deletedFiles = _fileHashes.Keys
                .Where(f => !allFiles.Contains(f))
                .ToList();

            stats.TotalFiles = changedFiles.Count + deletedFiles.Count;

            // Remove deleted files from index
            foreach (var file in deletedFiles)
            {
                if (ct.IsCancellationRequested) break;
                await RemoveFileFromIndexAsync(file, ct);
                _fileHashes.TryRemove(file, out _);
                stats.ProcessedFiles++;
            }

            // Index changed files
            foreach (var file in changedFiles)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    stats.CurrentFile = file;
                    await RemoveFileFromIndexAsync(file, ct);
                    var chunks = await IndexFileAsync(file, forceReindex: true, ct);
                    if (chunks > 0)
                    {
                        stats.ProcessedFiles++;
                        stats.IndexedChunks += chunks;
                        OnFileIndexed?.Invoke(file, chunks);
                    }
                    else
                    {
                        stats.SkippedFiles++;
                    }
                }
                catch (Exception ex)
                {
                    stats.ErrorFiles++;
                    Console.WriteLine($"[IndexError] {file}: {ex.Message}");
                }

                ReportProgress(progress, stats, file, startTime);
            }

            var finalProgress = CreateProgress(stats, startTime, isComplete: true);
            OnProgress?.Invoke(finalProgress);
            return finalProgress;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Indexes a specific file or directory.
    /// </summary>
    public async Task<int> IndexPathAsync(string path, CancellationToken ct = default)
    {
        if (Directory.Exists(path))
        {
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Where(f => ShouldIndexFile(f))
                .ToList();

            int totalChunks = 0;
            foreach (var file in files)
            {
                totalChunks += await IndexFileAsync(file, forceReindex: true, ct);
            }
            return totalChunks;
        }
        else if (File.Exists(path))
        {
            return await IndexFileAsync(path, forceReindex: true, ct);
        }

        return 0;
    }

    /// <summary>
    /// Searches the indexed content.
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(
        string query,
        int limit = 10,
        float scoreThreshold = 0.5f,
        CancellationToken ct = default)
    {
        var queryEmbedding = await _embedding.CreateEmbeddingsAsync(query, ct);

        var searchResults = await _client.SearchAsync(
            _config.CollectionName,
            queryEmbedding,
            limit: (ulong)limit,
            scoreThreshold: scoreThreshold,
            cancellationToken: ct);

        return searchResults.Select(r => new SearchResult
        {
            FilePath = r.Payload.TryGetValue("file_path", out var fp) ? fp.StringValue : string.Empty,
            ChunkIndex = r.Payload.TryGetValue("chunk_index", out var ci) ? (int)ci.IntegerValue : 0,
            Content = r.Payload.TryGetValue("content", out var c) ? c.StringValue : string.Empty,
            Score = r.Score
        }).ToList();
    }

    /// <summary>
    /// Gets statistics about the indexed content.
    /// </summary>
    public async Task<IndexStats> GetStatsAsync(CancellationToken ct = default)
    {
        try
        {
            var collectionInfo = await _client.GetCollectionInfoAsync(_config.CollectionName, ct);
            return new IndexStats
            {
                TotalVectors = (long)collectionInfo.PointsCount,
                IndexedFiles = _fileHashes.Count,
                CollectionName = _config.CollectionName,
                VectorSize = _config.VectorSize
            };
        }
        catch
        {
            return new IndexStats { CollectionName = _config.CollectionName };
        }
    }

    /// <summary>
    /// Adds a root path to monitor.
    /// </summary>
    public void AddRootPath(string path)
    {
        if (!Directory.Exists(path)) return;
        if (_config.RootPaths.Contains(path, StringComparer.OrdinalIgnoreCase)) return;

        _config.RootPaths.Add(path);

        if (_config.EnableFileWatcher && _isInitialized)
        {
            StartWatcherForPath(path);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _watcherCts.Cancel();

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        if (_debounceTask != null)
        {
            try { await _debounceTask; } catch { }
        }

        _watcherCts.Dispose();
        _indexLock.Dispose();
        _client.Dispose();
    }
}
