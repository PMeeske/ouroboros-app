// <copyright file="QdrantSelfIndexer.FileWatcher.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

/// <summary>
/// Partial class containing file watching, change processing,
/// and debounced update logic.
/// </summary>
public sealed partial class QdrantSelfIndexer
{
    private void StartFileWatchers()
    {
        foreach (var path in _config.RootPaths)
        {
            StartWatcherForPath(path);
        }

        // Start debounce processor
        _debounceTask = ProcessPendingChangesAsync(_watcherCts.Token);
    }

    private void StartWatcherForPath(string path)
    {
        if (!Directory.Exists(path)) return;

        try
        {
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;

            _watchers.Add(watcher);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WatcherError] {path}: {ex.Message}");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!ShouldIndexFile(e.FullPath)) return;
        _pendingChanges[e.FullPath] = DateTime.UtcNow;
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (!ShouldIndexFile(e.FullPath)) return;
        _pendingChanges[e.FullPath] = DateTime.MinValue; // Mark for deletion
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Mark old path for deletion
        if (ShouldIndexFile(e.OldFullPath))
        {
            _pendingChanges[e.OldFullPath] = DateTime.MinValue;
        }

        // Mark new path for indexing
        if (ShouldIndexFile(e.FullPath))
        {
            _pendingChanges[e.FullPath] = DateTime.UtcNow;
        }
    }

    private async Task ProcessPendingChangesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_config.FileWatcherDebounceMs, ct);

                var cutoff = DateTime.UtcNow.AddMilliseconds(-_config.FileWatcherDebounceMs);
                var toProcess = _pendingChanges
                    .Where(kvp => kvp.Value <= cutoff || kvp.Value == DateTime.MinValue)
                    .ToList();

                foreach (var (path, timestamp) in toProcess)
                {
                    _pendingChanges.TryRemove(path, out _);

                    if (timestamp == DateTime.MinValue)
                    {
                        // File deleted
                        await RemoveFileFromIndexAsync(path, ct);
                        _fileHashes.TryRemove(path, out _);
                        Console.WriteLine($"[IndexRemoved] {path}");
                    }
                    else
                    {
                        // File changed/created
                        await RemoveFileFromIndexAsync(path, ct);
                        var chunks = await IndexFileAsync(path, forceReindex: true, ct);
                        if (chunks > 0)
                        {
                            Console.WriteLine($"[IndexUpdated] {path} ({chunks} chunks)");
                            OnFileIndexed?.Invoke(path, chunks);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PendingChangesError] {ex.Message}");
            }
        }
    }
}
