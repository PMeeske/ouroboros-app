// <copyright file="ParallelMeTTaThoughtStreams.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Ouroboros.Application.Tools;

/// <summary>
/// Parallel MeTTa thought stream orchestrator that runs multiple symbolic reasoning
/// engines concurrently, producing interleaved thought streams with modulo-square
/// theory solving capabilities. Uses Ollama for neural inference fusion.
/// </summary>
public sealed partial class ParallelMeTTaThoughtStreams : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, MeTTaStreamNode> _nodes = new();
    private readonly Channel<ThoughtAtom> _mergedStream;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxParallelism;

    private Func<string, CancellationToken, Task<string>>? _ollamaInferenceFunc;
#pragma warning disable CS0414 // Field is assigned but never used - reserved for state management
    private bool _isRunning;
#pragma warning restore CS0414

    /// <summary>
    /// Event fired when a new thought atom is produced by any stream.
    /// </summary>
    public event Action<ThoughtAtom>? OnThoughtAtom;

    /// <summary>
    /// Event fired when streams converge on a shared conclusion.
    /// </summary>
    public event Action<ConvergenceEvent>? OnConvergence;

    /// <summary>
    /// Event fired when a modulo-square theory is solved.
    /// </summary>
    public event Action<ModuloSquareSolution>? OnTheorySolved;

    /// <summary>
    /// Gets all active stream node IDs.
    /// </summary>
    public IReadOnlyCollection<string> ActiveStreams => _nodes.Keys.ToList();

    /// <summary>
    /// Gets the merged thought stream for consumption.
    /// </summary>
    public ChannelReader<ThoughtAtom> MergedStream => _mergedStream.Reader;

    /// <summary>
    /// Creates a new parallel MeTTa thought stream orchestrator.
    /// </summary>
    /// <param name="maxParallelism">Maximum number of concurrent streams (default: processor count).</param>
    public ParallelMeTTaThoughtStreams(int maxParallelism = 0)
    {
        _maxParallelism = maxParallelism > 0 ? maxParallelism : Environment.ProcessorCount;
        _semaphore = new SemaphoreSlim(_maxParallelism);
        _mergedStream = Channel.CreateUnbounded<ThoughtAtom>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        });
    }

    /// <summary>
    /// Connects Ollama inference for neural-symbolic fusion.
    /// </summary>
    /// <param name="inferenceFunc">Function that takes a prompt and returns LLM response.</param>
    public void ConnectOllama(Func<string, CancellationToken, Task<string>> inferenceFunc)
    {
        _ollamaInferenceFunc = inferenceFunc;
    }

    /// <summary>
    /// Creates a new MeTTa stream node with its own knowledge base.
    /// </summary>
    /// <param name="streamId">Unique identifier for this stream.</param>
    /// <param name="seedAtoms">Initial atoms to seed the knowledge base.</param>
    /// <returns>The created stream node.</returns>
    public async Task<MeTTaStreamNode> CreateStreamAsync(string streamId, IEnumerable<string>? seedAtoms = null)
    {
        var engine = new InMemoryMeTTaEngine();
        var node = new MeTTaStreamNode(streamId, engine, this);

        if (seedAtoms != null)
        {
            foreach (var atom in seedAtoms)
            {
                _ = await engine.AddFactAsync(atom);
            }
        }

        _nodes[streamId] = node;
        return node;
    }

    /// <summary>
    /// Creates multiple streams with different theory seeds for parallel exploration.
    /// </summary>
    /// <param name="theories">Dictionary of stream ID to theory seed atoms.</param>
    /// <returns>List of created stream nodes.</returns>
    public async Task<List<MeTTaStreamNode>> CreateTheoryStreamsAsync(Dictionary<string, List<string>> theories)
    {
        var nodes = new List<MeTTaStreamNode>();
        foreach (var kvp in theories)
        {
            nodes.Add(await CreateStreamAsync(kvp.Key, kvp.Value));
        }
        return nodes;
    }

    /// <summary>
    /// Starts parallel thought generation across all streams.
    /// </summary>
    /// <param name="query">The query to explore across all streams.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task StartParallelThinkingAsync(string query, CancellationToken ct = default)
    {
        _isRunning = true;

        var tasks = _nodes.Values.Select(async node =>
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                await node.ThinkAsync(query, ct);
            }
            finally
            {
                _semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Streams thought atoms from all nodes as an async enumerable.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of thought atoms.</returns>
    public async IAsyncEnumerable<ThoughtAtom> StreamThoughtsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var atom in _mergedStream.Reader.ReadAllAsync(ct))
        {
            yield return atom;
        }
    }

    /// <summary>
    /// Emits a thought atom from a stream node.
    /// </summary>
    internal async Task EmitAtomAsync(ThoughtAtom atom, CancellationToken ct)
    {
        await _mergedStream.Writer.WriteAsync(atom, ct);
        OnThoughtAtom?.Invoke(atom);

        // Check for convergence across streams
        await CheckConvergenceAsync(atom, ct);
    }

    /// <summary>
    /// Checks if multiple streams have converged on similar conclusions.
    /// </summary>
    private async Task CheckConvergenceAsync(ThoughtAtom newAtom, CancellationToken ct)
    {
        var convergent = new List<(string streamId, ThoughtAtom atom)>();

        foreach (var node in _nodes.Values)
        {
            if (node.StreamId == newAtom.StreamId) continue;

            var recent = node.RecentAtoms.TakeLast(10);
            foreach (var atom in recent)
            {
                if (AtomsConverge(newAtom, atom))
                {
                    convergent.Add((node.StreamId, atom));
                }
            }
        }

        if (convergent.Count >= 2)
        {
            var convergence = new ConvergenceEvent
            {
                Timestamp = DateTime.UtcNow,
                TriggerAtom = newAtom,
                ConvergentStreams = convergent.Select(c => c.streamId).Distinct().ToList(),
                SharedConcept = ExtractSharedConcept(newAtom, convergent.Select(c => c.atom)),
            };

            OnConvergence?.Invoke(convergence);

            // Use Ollama to synthesize convergent insights
            if (_ollamaInferenceFunc != null)
            {
                var synthesis = await SynthesizeConvergenceAsync(convergence, ct);
                if (!string.IsNullOrEmpty(synthesis))
                {
                    // Broadcast synthesis back to all convergent streams
                    foreach (var streamId in convergence.ConvergentStreams)
                    {
                        if (_nodes.TryGetValue(streamId, out var node))
                        {
                            await node.InjectInsightAsync(synthesis, ct);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Determines if two atoms represent convergent reasoning.
    /// </summary>
    private static bool AtomsConverge(ThoughtAtom a, ThoughtAtom b)
    {
        if (a.StreamId == b.StreamId) return false;
        if (Math.Abs((a.Timestamp - b.Timestamp).TotalSeconds) > 30) return false;

        // Check concept overlap
        var aWords = a.Content.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bWords = b.Content.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var intersection = aWords.Intersect(bWords).Count();
        var union = aWords.Union(bWords).Count();

        // Jaccard similarity > 0.3 indicates convergence
        return union > 0 && (double)intersection / union > 0.3;
    }

    /// <summary>
    /// Extracts the shared concept from convergent atoms.
    /// </summary>
    private static string ExtractSharedConcept(ThoughtAtom trigger, IEnumerable<ThoughtAtom> convergent)
    {
        var allContent = new[] { trigger.Content }.Concat(convergent.Select(a => a.Content));
        var words = allContent
            .SelectMany(c => c.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key);

        return string.Join(" ", words);
    }

    /// <summary>
    /// Uses Ollama to synthesize insights from convergent streams.
    /// </summary>
    private async Task<string> SynthesizeConvergenceAsync(ConvergenceEvent convergence, CancellationToken ct)
    {
        if (_ollamaInferenceFunc == null) return "";

        var prompt = $@"Multiple parallel reasoning streams have converged on related concepts.

Trigger thought: {convergence.TriggerAtom.Content}
Convergent streams: {string.Join(", ", convergence.ConvergentStreams)}
Shared concept: {convergence.SharedConcept}

Synthesize a unified insight that combines these convergent thoughts into a single coherent conclusion.
Be brief (1-2 sentences) and focus on the emergent meaning.";

        return await _ollamaInferenceFunc(prompt, ct);
    }

    /// <summary>
    /// Gets statistics about all streams.
    /// </summary>
    public ParallelStreamStats GetStats()
    {
        return new ParallelStreamStats
        {
            ActiveStreams = _nodes.Count,
            TotalAtomsGenerated = _nodes.Values.Sum(n => n.AtomCount),
            ConvergenceEvents = 0, // Would need tracking
            StreamDetails = _nodes.Values.Select(n => new StreamDetail
            {
                StreamId = n.StreamId,
                AtomCount = n.AtomCount,
                LastActivity = n.LastActivity,
            }).ToList(),
        };
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _mergedStream.Writer.Complete();

        foreach (var node in _nodes.Values)
        {
            await node.DisposeAsync();
        }

        _nodes.Clear();
        _semaphore.Dispose();
        _cts.Dispose();
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\((?:square-root|solution)\s+(\d+)\)")]
    private static partial System.Text.RegularExpressions.Regex SolutionExtractRegex();
}