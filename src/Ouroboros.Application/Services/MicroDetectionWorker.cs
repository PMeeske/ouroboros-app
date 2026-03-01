// <copyright file="MicroDetectionWorker.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ouroboros.Application.Services;

/// <summary>
/// Dedicated offloaded worker for micro detection subsystems.
///
/// Hosts all pluggable <see cref="IDetectionModule"/> instances (presence, motion,
/// gesture, speech) on a long-lived background task. Detection results are published
/// to a bounded <see cref="Channel{T}"/> that consumers read asynchronously.
///
/// Design:
///   - Each module declares its own <see cref="IDetectionModule.Interval"/>.
///   - The worker ticks every 100ms and polls any modules whose interval has elapsed.
///   - All detection I/O runs on the worker task, keeping the main agent thread free.
///   - Clean shutdown via <see cref="StopAsync"/> completes the channel and disposes modules.
/// </summary>
public sealed class MicroDetectionWorker : IAsyncDisposable
{
    private readonly List<IDetectionModule> _modules = new();
    private readonly Channel<DetectionEvent> _channel;
    private readonly ILogger _logger;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;
    private bool _disposed;

    /// <summary>
    /// Consumer endpoint — read detection events from this stream.
    /// </summary>
    public ChannelReader<DetectionEvent> DetectionStream => _channel.Reader;

    /// <summary>
    /// Gets whether the worker is currently running.
    /// </summary>
    public bool IsRunning => _workerTask is { IsCompleted: false };

    /// <summary>
    /// Gets the number of registered detection modules.
    /// </summary>
    public int ModuleCount => _modules.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="MicroDetectionWorker"/> class.
    /// </summary>
    /// <param name="channelCapacity">Bounded channel capacity (default 500).</param>
    /// <param name="logger">Optional logger.</param>
    public MicroDetectionWorker(int channelCapacity = 500, ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _channel = Channel.CreateBounded<DetectionEvent>(
            new BoundedChannelOptions(channelCapacity)
            {
                SingleReader = false,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest,
            });
    }

    /// <summary>
    /// Registers a detection module. Must be called before <see cref="StartAsync"/>.
    /// </summary>
    public void RegisterModule(IDetectionModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (_workerTask != null)
            throw new InvalidOperationException("Cannot register modules after the worker has started.");
        _modules.Add(module);
        _logger.LogDebug("[MicroDetection] Registered module: {Name} (interval: {Interval}s)",
            module.Name, module.Interval.TotalSeconds);
    }

    /// <summary>
    /// Starts the worker loop on a dedicated long-running task.
    /// </summary>
    public void StartAsync()
    {
        if (_workerTask != null)
            throw new InvalidOperationException("Worker is already running.");
        if (_modules.Count == 0)
        {
            _logger.LogWarning("[MicroDetection] No modules registered. Worker will not start.");
            return;
        }

        _cts = new CancellationTokenSource();
        _workerTask = Task.Factory.StartNew(
            () => WorkerLoopAsync(_cts.Token),
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();

        _logger.LogInformation("[MicroDetection] Worker started with {Count} modules", _modules.Count);
    }

    /// <summary>
    /// Gracefully stops the worker and completes the detection channel.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts == null || _workerTask == null) return;

        _cts.Cancel();
        try
        {
            await _workerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        finally
        {
            _channel.Writer.TryComplete();
            _logger.LogInformation("[MicroDetection] Worker stopped");
        }
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        var tickInterval = TimeSpan.FromMilliseconds(100);

        while (!ct.IsCancellationRequested)
        {
            foreach (var module in _modules)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    if (!module.IsReady()) continue;

                    var evt = await module.DetectAsync(ct).ConfigureAwait(false);
                    if (evt != null)
                    {
                        await _channel.Writer.WriteAsync(evt, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
        catch (IOException ex)
                {
                    _logger.LogWarning(
                        "[MicroDetection] Module '{Module}' error: {Message}",
                        module.Name, ex.Message);
                }
            }

            try
            {
                await Task.Delay(tickInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync().ConfigureAwait(false);

        _cts?.Dispose();

        foreach (var module in _modules)
        {
            try { module.Dispose(); }
        catch (IOException ex)
            {
                _logger.LogWarning("[MicroDetection] Error disposing module '{Module}': {Message}",
                    module.Name, ex.Message);
            }
        }
        _modules.Clear();

        GC.SuppressFinalize(this);
    }
}
