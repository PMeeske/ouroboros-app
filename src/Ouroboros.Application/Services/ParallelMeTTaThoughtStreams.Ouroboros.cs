// <copyright file="ParallelMeTTaThoughtStreams.Ouroboros.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Runtime.CompilerServices;
using Ouroboros.Application.Extensions;
using Ouroboros.Application.Tools;

/// <summary>
/// Partial class containing Ouroboros atom operations: self-referential streams,
/// networks, strange loops, and merging.
/// </summary>
public sealed partial class ParallelMeTTaThoughtStreams
{
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
            EmitAtomAsync(thought, CancellationToken.None).ObserveExceptions("OuroborosStream.SelfConsumption");
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
            EmitAtomAsync(thought, CancellationToken.None).ObserveExceptions("OuroborosStream.FixedPoint");
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
            current.Node.InjectInsightAsync(
                $"(neighbor-awareness (observe {prev.Atom.Id}) (observe {next.Atom.Id}))",
                CancellationToken.None).ObserveExceptions("OuroborosNetwork.NeighborAwareness");
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
}
