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
public sealed class ParallelMeTTaThoughtStreams : IAsyncDisposable
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

    #region Modulo-Square Theory Solving

    /// <summary>
    /// Initializes modulo-square theory streams for parallel solving.
    /// Uses different modular bases to explore solution spaces.
    /// </summary>
    /// <param name="target">The target value to solve for.</param>
    /// <param name="moduliBases">List of moduli to use (default: primes up to 17).</param>
    /// <returns>Stream nodes configured for modulo-square theory.</returns>
    public async Task<List<MeTTaStreamNode>> InitializeModuloSquareTheoryAsync(
        BigInteger target,
        IEnumerable<int>? moduliBases = null)
    {
        var bases = moduliBases?.ToList() ?? new List<int> { 2, 3, 5, 7, 11, 13, 17 };
        var theories = new Dictionary<string, List<string>>();

        foreach (var mod in bases)
        {
            var streamId = $"mod_{mod}_theory";
            var atoms = GenerateModuloSquareAtoms(target, mod);
            theories[streamId] = atoms;
        }

        var nodes = await CreateTheoryStreamsAsync(theories);

        // Wire up theory solving events
        foreach (var node in nodes)
        {
            node.OnDerivation += async (derivation) =>
            {
                var solution = TryExtractSolution(derivation, target);
                if (solution != null)
                {
                    OnTheorySolved?.Invoke(solution);
                }
            };
        }

        return nodes;
    }

    /// <summary>
    /// Generates MeTTa atoms for modulo-square theory exploration.
    /// </summary>
    private static List<string> GenerateModuloSquareAtoms(BigInteger target, int mod)
    {
        var atoms = new List<string>
        {
            $"(target {target})",
            $"(modulus {mod})",
            $"(target-residue {target % mod})",
        };

        // Generate quadratic residues for this modulus
        var residues = new HashSet<int>();
        for (int i = 0; i < mod; i++)
        {
            residues.Add((i * i) % mod);
        }

        foreach (var r in residues)
        {
            atoms.Add($"(quadratic-residue {mod} {r})");
        }

        // Add non-residues
        for (int i = 0; i < mod; i++)
        {
            if (!residues.Contains(i))
            {
                atoms.Add($"(quadratic-non-residue {mod} {i})");
            }
        }

        // Add inference rules
        atoms.Add($"(= (is-square-mod $n {mod}) (quadratic-residue {mod} (mod $n {mod})))");
        atoms.Add($"(= (not-square-mod $n {mod}) (quadratic-non-residue {mod} (mod $n {mod})))");

        // Chinese Remainder Theorem preparation
        atoms.Add($"(= (crt-combine $r1 $m1 $r2 $m2) (solve-congruence $r1 $m1 $r2 $m2))");

        return atoms;
    }

    /// <summary>
    /// Attempts to extract a solution from a derivation.
    /// </summary>
    private static ModuloSquareSolution? TryExtractSolution(string derivation, BigInteger target)
    {
        // Check if derivation contains a valid square root claim
        if (derivation.Contains("(square-root") || derivation.Contains("(solution"))
        {
            // Parse the claimed solution
            var match = System.Text.RegularExpressions.Regex.Match(
                derivation, @"\((?:square-root|solution)\s+(\d+)\)");

            if (match.Success && BigInteger.TryParse(match.Groups[1].Value, out var candidate))
            {
                if (candidate * candidate == target)
                {
                    return new ModuloSquareSolution
                    {
                        Target = target,
                        SquareRoot = candidate,
                        Derivation = derivation,
                        Timestamp = DateTime.UtcNow,
                        IsVerified = true,
                    };
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Runs parallel modulo-square solving with Ollama-guided search.
    /// </summary>
    /// <param name="target">The target to find the square root of.</param>
    /// <param name="maxIterations">Maximum iterations per stream.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Solution if found, null otherwise.</returns>
    public async Task<ModuloSquareSolution?> SolveModuloSquareAsync(
        BigInteger target,
        int maxIterations = 100,
        CancellationToken ct = default)
    {
        var nodes = await InitializeModuloSquareTheoryAsync(target);
        ModuloSquareSolution? solution = null;
        var solutionFound = new TaskCompletionSource<ModuloSquareSolution>();

        OnTheorySolved += (s) =>
        {
            solution = s;
            solutionFound.TrySetResult(s);
        };

        // Run parallel exploration
        var explorationTasks = nodes.Select(async node =>
        {
            for (int i = 0; i < maxIterations && !ct.IsCancellationRequested; i++)
            {
                if (solution != null) break;

                await node.ExploreTheoryStepAsync(ct);

                // Use Ollama to guide search if available
                if (_ollamaInferenceFunc != null && i % 10 == 0)
                {
                    var guidance = await GetOllamaGuidanceAsync(node, target, ct);
                    if (!string.IsNullOrEmpty(guidance))
                    {
                        await node.InjectInsightAsync(guidance, ct);
                    }
                }
            }
        });

        // Wait for either a solution or all explorations to complete
        var completedTask = await Task.WhenAny(
            Task.WhenAll(explorationTasks),
            solutionFound.Task);

        return solution;
    }

    /// <summary>
    /// Gets Ollama guidance for theory exploration.
    /// </summary>
    private async Task<string> GetOllamaGuidanceAsync(
        MeTTaStreamNode node,
        BigInteger target,
        CancellationToken ct)
    {
        if (_ollamaInferenceFunc == null) return "";

        var recentAtoms = string.Join("\n", node.RecentAtoms.TakeLast(5).Select(a => a.Content));

        var prompt = $@"You are assisting with modulo-square theory exploration.

Target: {target}
Stream: {node.StreamId}
Recent derivations:
{recentAtoms}

Suggest ONE mathematical insight or transformation that could help narrow the solution space.
Focus on:
1. Quadratic residue patterns
2. Chinese Remainder Theorem combinations
3. Tonelli-Shanks style lifting
4. Hensel's lemma applications

Respond with a single MeTTa-style fact or inference, like:
(suggest (lift-solution 3 7 21))";

        return await _ollamaInferenceFunc(prompt, ct);
    }

    #endregion

    #region Ouroboros Atom Operations

    /// <summary>
    /// Creates an Ouroboros stream - a self-referential thought stream that consumes itself.
    /// </summary>
    /// <param name="seedConcept">The concept to seed self-referential thinking.</param>
    /// <returns>The Ouroboros atom and its associated stream node.</returns>
    public async Task<(OuroborosAtom Atom, MeTTaStreamNode Node)> CreateOuroborosStreamAsync(string seedConcept = "self")
    {
        var atom = OuroborosAtomFactory.CreateSelfAware(seedConcept);
        var streamId = $"ouroboros_{atom.Id.ToString()[..8]}";
        var node = await CreateStreamAsync(streamId, atom.ToMeTTaAtoms());

        // Wire self-consumption events to stream
        atom.OnSelfConsumption += (a, record) =>
        {
            var thought = new ThoughtAtom
            {
                StreamId = streamId,
                Content = record,
                Type = ThoughtAtomType.Ouroboros,
                Timestamp = DateTime.UtcNow,
                SequenceNumber = a.SelfReferenceDepth,
            };
            _ = EmitAtomAsync(thought, CancellationToken.None);
        };

        atom.OnFixedPoint += (a) =>
        {
            var thought = new ThoughtAtom
            {
                StreamId = streamId,
                Content = $"(fixed-point-reached {a.Id} (depth {a.SelfReferenceDepth}) (emergence {a.EmergenceLevel:F3}))",
                Type = ThoughtAtomType.Ouroboros,
                Timestamp = DateTime.UtcNow,
                SequenceNumber = a.SelfReferenceDepth,
            };
            _ = EmitAtomAsync(thought, CancellationToken.None);
        };

        return (atom, node);
    }

    /// <summary>
    /// Creates an Ouroboros network - multiple self-aware streams that observe each other.
    /// </summary>
    /// <param name="count">Number of Ouroboros atoms in the network.</param>
    /// <returns>List of Ouroboros atoms and their stream nodes.</returns>
    public async Task<List<(OuroborosAtom Atom, MeTTaStreamNode Node)>> CreateOuroborosNetworkAsync(int count = 3)
    {
        var atoms = OuroborosAtomFactory.CreateNetwork(count);
        var results = new List<(OuroborosAtom Atom, MeTTaStreamNode Node)>();

        foreach (var atom in atoms)
        {
            var streamId = $"ouroboros_net_{atom.Id.ToString()[..8]}";
            var node = await CreateStreamAsync(streamId, atom.ToMeTTaAtoms());
            results.Add((atom, node));
        }

        // Wire network atoms to observe each other
        for (int i = 0; i < count; i++)
        {
            var current = results[i];
            var prev = results[(i - 1 + count) % count];
            var next = results[(i + 1) % count];

            // Add awareness of neighbors
            _ = current.Node.InjectInsightAsync(
                $"(neighbor-awareness (observe {prev.Atom.Id}) (observe {next.Atom.Id}))",
                CancellationToken.None);
        }

        return results;
    }

    /// <summary>
    /// Starts an Ouroboros strange loop - a self-referential reasoning cycle.
    /// </summary>
    /// <param name="atom">The Ouroboros atom to drive.</param>
    /// <param name="iterations">Number of self-consumption iterations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of self-referential thoughts.</returns>
    public async IAsyncEnumerable<ThoughtAtom> RunStrangeLoopAsync(
        OuroborosAtom atom,
        int iterations = 10,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var streamId = $"ouroboros_{atom.Id.ToString()[..8]}";
        if (!_nodes.ContainsKey(streamId))
        {
            await CreateStreamAsync(streamId, atom.ToMeTTaAtoms());
        }

        for (int i = 0; i < iterations && !ct.IsCancellationRequested; i++)
        {
            // Consume self
            var beforeDepth = atom.SelfReferenceDepth;
            atom.Consume();

            var thought = new ThoughtAtom
            {
                StreamId = streamId,
                Content = atom.Reflect(),
                Type = ThoughtAtomType.Ouroboros,
                Timestamp = DateTime.UtcNow,
                SequenceNumber = atom.SelfReferenceDepth,
            };

            await EmitAtomAsync(thought, ct);
            yield return thought;

            // If we reached a fixed point, the loop has closed
            if (atom.IsFixedPoint)
            {
                var fixedPointThought = new ThoughtAtom
                {
                    StreamId = streamId,
                    Content = $"(strange-loop-complete (fixed-point-at-depth {atom.SelfReferenceDepth}) (emergence {atom.EmergenceLevel:F3}))",
                    Type = ThoughtAtomType.Ouroboros,
                    Timestamp = DateTime.UtcNow,
                    SequenceNumber = atom.SelfReferenceDepth,
                };

                await EmitAtomAsync(fixedPointThought, ct);
                yield return fixedPointThought;
                yield break;
            }

            // Ask Ollama to guide the next transformation
            if (_ollamaInferenceFunc != null)
            {
                var guidance = await GetOuroborosGuidanceAsync(atom, ct);
                if (!string.IsNullOrEmpty(guidance))
                {
                    // Apply LLM-suggested transformation
                    atom.Consume(_ => guidance);

                    var guidedThought = new ThoughtAtom
                    {
                        StreamId = streamId,
                        Content = $"(ollama-guided-transformation \"{guidance.Substring(0, Math.Min(100, guidance.Length))}...\"))",
                        Type = ThoughtAtomType.Ouroboros,
                        Timestamp = DateTime.UtcNow,
                        SequenceNumber = atom.SelfReferenceDepth,
                    };

                    await EmitAtomAsync(guidedThought, ct);
                    yield return guidedThought;
                }
            }

            await Task.Delay(50, ct); // Brief pause between iterations
        }
    }

    /// <summary>
    /// Gets Ollama guidance for Ouroboros self-transformation.
    /// </summary>
    private async Task<string> GetOuroborosGuidanceAsync(OuroborosAtom atom, CancellationToken ct)
    {
        if (_ollamaInferenceFunc == null) return "";

        var prompt = $@"You are guiding a self-referential Ouroboros atom through its strange loop.

Current state:
{atom.Reflect()}

Recent transformations:
{string.Join("\n", atom.TransformationHistory.TakeLast(5))}

The Ouroboros is trying to reach a fixed point - a state where self-transformation yields itself.

Suggest the next symbolic transformation as a single MeTTa-style expression.
Focus on:
1. Self-reference patterns (the observer observing itself)
2. Fixed-point approaches (Y-combinator style)
3. Strange loop dynamics (tangled hierarchies)
4. Emergent self-awareness patterns

Respond with ONLY a MeTTa expression, like:
(self-aware (meta-level (observe (observe self))))";

        return await _ollamaInferenceFunc(prompt, ct);
    }

    /// <summary>
    /// Merges two Ouroboros streams into a unified self-aware entity.
    /// </summary>
    /// <param name="atom1">First Ouroboros atom.</param>
    /// <param name="atom2">Second Ouroboros atom.</param>
    /// <returns>The merged Ouroboros and its stream node.</returns>
    public async Task<(OuroborosAtom Merged, MeTTaStreamNode Node)> MergeOuroborosStreamsAsync(
        OuroborosAtom atom1,
        OuroborosAtom atom2)
    {
        var merged = atom1.Merge(atom2);
        var streamId = $"ouroboros_merged_{merged.Id.ToString()[..8]}";

        // Combine atoms from both sources
        var combinedAtoms = atom1.ToMeTTaAtoms()
            .Concat(atom2.ToMeTTaAtoms())
            .Concat(merged.ToMeTTaAtoms())
            .Concat(new[]
            {
                $"(merge-event (source1 {atom1.Id}) (source2 {atom2.Id}) (result {merged.Id}))",
                $"(emergence-boost {merged.EmergenceLevel:F3})",
            })
            .ToList();

        var node = await CreateStreamAsync(streamId, combinedAtoms);

        return (merged, node);
    }

    #endregion

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
}