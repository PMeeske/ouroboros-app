using System.Collections.Concurrent;

namespace Ouroboros.Application.Personality;

/// <summary>
/// Engine that executes useful background operations based on autonomous thoughts,
/// synergizing with the active conversation to prepare relevant information.
/// </summary>
public sealed class ThoughtDrivenOperationEngine
{
    private readonly List<IBackgroundOperationExecutor> _executors = [];
    private readonly ConcurrentQueue<BackgroundOperationResult> _completedOperations = new();
    private readonly ConcurrentDictionary<string, object> _prefetchedData = new();
    private readonly object _contextLock = new();
    private BackgroundOperationContext? _currentContext;

    private const int MaxCompletedOperations = 50;
    private const int MaxPrefetchedItems = 100;

    /// <summary>
    /// Registers a background operation executor.
    /// </summary>
    public void RegisterExecutor(IBackgroundOperationExecutor executor)
    {
        _executors.Add(executor);
    }

    /// <summary>
    /// Updates the current conversation context for background operations.
    /// </summary>
    public void UpdateContext(BackgroundOperationContext context)
    {
        lock (_contextLock)
        {
            _currentContext = context;
        }
    }

    /// <summary>
    /// Gets the current context.
    /// </summary>
    public BackgroundOperationContext? GetCurrentContext()
    {
        lock (_contextLock)
        {
            return _currentContext;
        }
    }

    /// <summary>
    /// Processes a thought and executes relevant background operations.
    /// </summary>
    public async Task<List<BackgroundOperationResult>> ProcessThoughtAsync(
        InnerThought thought,
        CancellationToken ct = default)
    {
        var results = new List<BackgroundOperationResult>();
        var context = GetCurrentContext();

        if (context == null) return results;

        foreach (var executor in _executors)
        {
            if (ct.IsCancellationRequested) break;

            if (executor.ShouldExecute(thought.Type, context))
            {
                try
                {
                    var result = await executor.ExecuteAsync(thought, context, ct);
                    if (result != null)
                    {
                        results.Add(result);
                        _completedOperations.Enqueue(result);

                        // Trim if needed
                        while (_completedOperations.Count > MaxCompletedOperations)
                        {
                            _completedOperations.TryDequeue(out _);
                        }

                        // Store prefetched data
                        if (result.Data != null && result.Success)
                        {
                            var key = $"{result.OperationType}:{result.OperationName}";
                            _prefetchedData[key] = result.Data;

                            while (_prefetchedData.Count > MaxPrefetchedItems)
                            {
                                var oldest = _prefetchedData.Keys.FirstOrDefault();
                                if (oldest != null) _prefetchedData.TryRemove(oldest, out _);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new BackgroundOperationResult(
                        "error", executor.Name, false, ex.Message, null,
                        TimeSpan.Zero, thought.Type));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Gets completed background operations.
    /// </summary>
    public List<BackgroundOperationResult> GetCompletedOperations(int limit = 10)
    {
        return _completedOperations.TakeLast(limit).ToList();
    }

    /// <summary>
    /// Gets prefetched data by key pattern.
    /// </summary>
    public Dictionary<string, object> GetPrefetchedData(string? keyPattern = null)
    {
        if (string.IsNullOrEmpty(keyPattern))
        {
            return new Dictionary<string, object>(_prefetchedData);
        }

        return _prefetchedData
            .Where(kv => kv.Key.Contains(keyPattern, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// Clears prefetched data older than the specified age.
    /// </summary>
    public void ClearStaleData(TimeSpan maxAge)
    {
        // In a real implementation, we'd track timestamps per entry
        // For now, just clear oldest entries if over limit
        while (_prefetchedData.Count > MaxPrefetchedItems / 2)
        {
            var oldest = _prefetchedData.Keys.FirstOrDefault();
            if (oldest != null) _prefetchedData.TryRemove(oldest, out _);
        }
    }
}