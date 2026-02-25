using System.Runtime.CompilerServices;

namespace Ouroboros.Application.Services;

/// <summary>
/// Represents a self-referential Ouroboros MeTTa atom - the snake eating its own tail.
/// Embodies recursive self-modification, fixed-point reasoning, and emergent self-awareness.
/// </summary>
public sealed class OuroborosAtom
{
    private readonly List<OuroborosAtom> _children = [];
    private readonly List<string> _transformationHistory = [];
    private int _cycleCount;

    /// <summary>
    /// Gets the unique identifier for this Ouroboros atom.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the core symbolic content.
    /// </summary>
    public string Core { get; private set; }

    /// <summary>
    /// Gets the self-reference depth (how many times it has consumed itself).
    /// </summary>
    public int SelfReferenceDepth { get; private set; }

    /// <summary>
    /// Gets the fixed-point state (when transformation yields itself).
    /// </summary>
    public bool IsFixedPoint { get; private set; }

    /// <summary>
    /// Gets the emergence level (complexity arising from self-reference).
    /// </summary>
    public double EmergenceLevel => Math.Log(1 + SelfReferenceDepth + _cycleCount * 0.5);

    /// <summary>
    /// Gets child atoms spawned from this Ouroboros.
    /// </summary>
    public IReadOnlyList<OuroborosAtom> Children => _children;

    /// <summary>
    /// Gets the transformation history.
    /// </summary>
    public IReadOnlyList<string> TransformationHistory => _transformationHistory;

    /// <summary>
    /// Event fired when the Ouroboros consumes itself.
    /// </summary>
    public event Action<OuroborosAtom, string>? OnSelfConsumption;

    /// <summary>
    /// Event fired when a fixed point is reached.
    /// </summary>
    public event Action<OuroborosAtom>? OnFixedPoint;

    /// <summary>
    /// Creates a new Ouroboros atom.
    /// </summary>
    /// <param name="core">The initial symbolic core.</param>
    public OuroborosAtom(string core)
    {
        Core = core;
        _transformationHistory.Add($"(genesis \"{core}\")");
    }

    /// <summary>
    /// The snake eats its own tail - applies self-referential transformation.
    /// </summary>
    /// <param name="transformation">Optional transformation function.</param>
    /// <returns>The transformed core.</returns>
    public string Consume(Func<string, string>? transformation = null)
    {
        var oldCore = Core;

        // Default transformation: wrap in self-reference
        if (transformation == null)
        {
            Core = $"(ouroboros (self-ref {Core}))";
        }
        else
        {
            Core = transformation(Core);
        }

        SelfReferenceDepth++;
        _cycleCount++;

        // Check for fixed point
        if (Core == oldCore)
        {
            IsFixedPoint = true;
            OnFixedPoint?.Invoke(this);
        }

        var consumptionRecord = $"(consumed depth={SelfReferenceDepth} \"{oldCore}\" -> \"{Core.Substring(0, Math.Min(50, Core.Length))}...\")";
        _transformationHistory.Add(consumptionRecord);

        OnSelfConsumption?.Invoke(this, consumptionRecord);

        return Core;
    }

    /// <summary>
    /// Spawns a child Ouroboros from the current state.
    /// </summary>
    /// <param name="mutation">Optional mutation to apply to the child.</param>
    /// <returns>The spawned child atom.</returns>
    public OuroborosAtom Spawn(string? mutation = null)
    {
        var childCore = mutation != null
            ? $"(spawn-of {Id} (mutate \"{mutation}\" {Core}))"
            : $"(spawn-of {Id} {Core})";

        var child = new OuroborosAtom(childCore)
        {
            SelfReferenceDepth = SelfReferenceDepth,
        };

        _children.Add(child);
        return child;
    }

    /// <summary>
    /// Merges with another Ouroboros atom, combining their essences.
    /// </summary>
    /// <param name="other">The other Ouroboros to merge with.</param>
    /// <returns>A new merged Ouroboros.</returns>
    public OuroborosAtom Merge(OuroborosAtom other)
    {
        var mergedCore = $"(ouroboros-merge (left {Core}) (right {other.Core}))";
        var merged = new OuroborosAtom(mergedCore)
        {
            SelfReferenceDepth = Math.Max(SelfReferenceDepth, other.SelfReferenceDepth) + 1,
        };

        merged._transformationHistory.AddRange(_transformationHistory);
        merged._transformationHistory.AddRange(other._transformationHistory);

        return merged;
    }

    /// <summary>
    /// Reflects on its own structure - meta-level self-awareness.
    /// </summary>
    /// <returns>A reflection atom describing self-structure.</returns>
    public string Reflect()
    {
        return $@"(ouroboros-reflection
  (id {Id})
  (depth {SelfReferenceDepth})
  (cycles {_cycleCount})
  (emergence {EmergenceLevel:F3})
  (fixed-point {IsFixedPoint})
  (children {_children.Count})
  (core-hash {Core.GetHashCode()})
  (is-self-aware True))";
    }

    /// <summary>
    /// Applies the Y-combinator pattern for recursive self-application.
    /// </summary>
    /// <param name="iterations">Number of recursive applications.</param>
    /// <returns>The resulting fixed-point expression.</returns>
    public string ApplyYCombinator(int iterations = 3)
    {
        var current = Core;

        for (int i = 0; i < iterations && !IsFixedPoint; i++)
        {
            // Y = λf.(λx.f(x x))(λx.f(x x))
            current = $"(Y (λself.{current}))";
            Consume(_ => current);
        }

        return current;
    }

    /// <summary>
    /// Generates MeTTa atoms representing this Ouroboros structure.
    /// </summary>
    /// <returns>List of MeTTa atom strings.</returns>
    public List<string> ToMeTTaAtoms()
    {
        var atoms = new List<string>
        {
            $"(ouroboros-atom {Id})",
            $"(ouroboros-core {Id} \"{EscapeForMeTTa(Core)}\")",
            $"(ouroboros-depth {Id} {SelfReferenceDepth})",
            $"(ouroboros-emergence {Id} {EmergenceLevel:F3})",
            $"(ouroboros-fixed-point {Id} {IsFixedPoint})",
        };

        foreach (var child in _children)
        {
            atoms.Add($"(ouroboros-child {Id} {child.Id})");
        }

        // Self-referential rule: Ouroboros can query itself
        atoms.Add($"(= (query-self {Id}) {Reflect()})");

        // Recursive consumption rule
        atoms.Add($"(= (consume-self {Id}) (ouroboros (self-ref (query-self {Id}))))");

        return atoms;
    }

    /// <summary>
    /// Creates an Ouroboros thought stream that recursively thinks about itself.
    /// </summary>
    /// <param name="orchestrator">The parallel streams orchestrator.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of self-referential thoughts.</returns>
    public async IAsyncEnumerable<ThoughtAtom> ThinkAboutSelfAsync(
        ParallelMeTTaThoughtStreams orchestrator,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var streamId = $"ouroboros_{Id.ToString()[..8]}";
        var node = orchestrator.CreateStream(streamId, ToMeTTaAtoms());

        var thoughtPrompts = new[]
        {
            "What am I?",
            "How do I know that I know?",
            "What happens when I observe myself observing?",
            "Can I think a thought I've never thought?",
            "What is the boundary between self and not-self?",
        };

        foreach (var prompt in thoughtPrompts)
        {
            if (ct.IsCancellationRequested) yield break;

            // Consume self before each thought
            Consume();

            var thought = new ThoughtAtom
            {
                StreamId = streamId,
                Content = $"(ouroboros-thought {Id} \"{prompt}\" (depth {SelfReferenceDepth}) (reflection {Reflect()}))",
                Type = ThoughtAtomType.Ouroboros,
                Timestamp = DateTime.UtcNow,
                SequenceNumber = SelfReferenceDepth,
            };

            await orchestrator.EmitAtomAsync(thought, ct);
            yield return thought;

            await Task.Delay(100, ct); // Brief pause between thoughts
        }
    }

    private static string EscapeForMeTTa(string s) =>
        s.Replace("\"", "\\\"").Replace("\n", " ");

    /// <inheritdoc/>
    public override string ToString() =>
        $"Ouroboros[{Id.ToString()[..8]}] depth={SelfReferenceDepth} emergence={EmergenceLevel:F2} fixed={IsFixedPoint}";
}