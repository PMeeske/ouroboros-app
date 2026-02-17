using System.Collections.Concurrent;
using System.Numerics;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Application.Services;

/// <summary>
/// Represents a single MeTTa stream node with its own knowledge base.
/// </summary>
public sealed class MeTTaStreamNode : IAsyncDisposable
{
    private readonly IMeTTaEngine _engine;
    private readonly ParallelMeTTaThoughtStreams _orchestrator;
    private readonly ConcurrentQueue<ThoughtAtom> _recentAtoms = new();
    private readonly Random _random = new();

    /// <summary>
    /// Gets the stream identifier.
    /// </summary>
    public string StreamId { get; }

    /// <summary>
    /// Gets the count of atoms generated.
    /// </summary>
    public int AtomCount { get; private set; }

    /// <summary>
    /// Gets the last activity timestamp.
    /// </summary>
    public DateTime LastActivity { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets recent atoms from this stream.
    /// </summary>
    public IEnumerable<ThoughtAtom> RecentAtoms => _recentAtoms;

    /// <summary>
    /// Event fired when a derivation is made.
    /// </summary>
    public event Func<string, Task>? OnDerivation;

    /// <summary>
    /// Creates a new stream node.
    /// </summary>
    internal MeTTaStreamNode(
        string streamId,
        IMeTTaEngine engine,
        ParallelMeTTaThoughtStreams orchestrator)
    {
        StreamId = streamId;
        _engine = engine;
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Generates thoughts based on a query.
    /// </summary>
    public async Task ThinkAsync(string query, CancellationToken ct = default)
    {
        // Add query as a goal
        await _engine.AddFactAsync($"(goal \"{query}\")", ct);

        // Generate initial thought based on query
        var queryResult = await _engine.ExecuteQueryAsync($"!(match &self (goal $g) $g)", ct);

        if (queryResult.IsSuccess)
        {
            await EmitThoughtAsync(queryResult.Value, ThoughtAtomType.Query, ct);
        }

        // Perform derivations
        for (int i = 0; i < 5 && !ct.IsCancellationRequested; i++)
        {
            await DeriveAsync(ct);
        }
    }

    /// <summary>
    /// Performs a single derivation step.
    /// </summary>
    public async Task DeriveAsync(CancellationToken ct = default)
    {
        // Query existing facts
        var facts = await _engine.ExecuteQueryAsync("!(match &self $fact $fact)", ct);

        if (facts.IsSuccess && !string.IsNullOrWhiteSpace(facts.Value))
        {
            // Apply inference
            var derivation = $"(derived-from {StreamId} {facts.Value.Split('\n').FirstOrDefault()})";
            await _engine.AddFactAsync(derivation, ct);
            await EmitThoughtAsync(derivation, ThoughtAtomType.Derivation, ct);

            OnDerivation?.Invoke(derivation);
        }
    }

    /// <summary>
    /// Explores a theory step for modulo-square solving.
    /// </summary>
    public async Task ExploreTheoryStepAsync(CancellationToken ct = default)
    {
        // Query for quadratic residues
        var residues = await _engine.ExecuteQueryAsync(
            "!(match &self (quadratic-residue $m $r) ($m $r))", ct);

        if (residues.IsSuccess)
        {
            // Generate exploration atom
            var exploration = $"(explored {StreamId} residue-space {DateTime.UtcNow.Ticks})";
            await _engine.AddFactAsync(exploration, ct);
            await EmitThoughtAsync(exploration, ThoughtAtomType.Exploration, ct);

            // Try lifting solutions
            var targetResult = await _engine.ExecuteQueryAsync(
                "!(match &self (target $t) $t)", ct);

            if (targetResult.IsSuccess && BigInteger.TryParse(targetResult.Value.Trim(), out var target))
            {
                // Attempt Tonelli-Shanks style derivation
                await AttemptTonelliShanksAsync(target, ct);
            }
        }
    }

    /// <summary>
    /// Attempts Tonelli-Shanks algorithm derivation.
    /// </summary>
    private async Task AttemptTonelliShanksAsync(BigInteger target, CancellationToken ct)
    {
        var modResult = await _engine.ExecuteQueryAsync(
            "!(match &self (modulus $m) $m)", ct);

        if (modResult.IsSuccess && int.TryParse(modResult.Value.Trim(), out var mod))
        {
            // Check if target is a quadratic residue
            var residue = (int)(target % mod);
            var isResidueResult = await _engine.ExecuteQueryAsync(
                $"!(match &self (quadratic-residue {mod} {residue}) True)", ct);

            if (isResidueResult.IsSuccess && isResidueResult.Value.Contains("True"))
            {
                // Target is a QR for this modulus - derive potential root
                var sqrtMod = TonelliShanks(residue, mod);
                if (sqrtMod.HasValue)
                {
                    var derivation = $"(partial-solution (sqrt {target} mod {mod}) = {sqrtMod.Value})";
                    await _engine.AddFactAsync(derivation, ct);
                    await EmitThoughtAsync(derivation, ThoughtAtomType.Solution, ct);
                }
            }
        }
    }

    /// <summary>
    /// Tonelli-Shanks algorithm for modular square roots.
    /// </summary>
    private static int? TonelliShanks(int n, int p)
    {
        if (p == 2) return n % 2;
        if (n == 0) return 0;

        // Check if n is a quadratic residue
        if (ModPow(n, (p - 1) / 2, p) != 1) return null;

        // Find Q and S such that p - 1 = Q * 2^S
        int q = p - 1;
        int s = 0;
        while (q % 2 == 0)
        {
            q /= 2;
            s++;
        }

        if (s == 1)
        {
            return ModPow(n, (p + 1) / 4, p);
        }

        // Find a quadratic non-residue z
        int z = 2;
        while (ModPow(z, (p - 1) / 2, p) != p - 1)
        {
            z++;
        }

        int m = s;
        int c = ModPow(z, q, p);
        int t = ModPow(n, q, p);
        int r = ModPow(n, (q + 1) / 2, p);

        while (true)
        {
            if (t == 1) return r;

            int i = 1;
            int temp = (t * t) % p;
            while (temp != 1)
            {
                temp = (temp * temp) % p;
                i++;
            }

            int b = ModPow(c, 1 << (m - i - 1), p);
            m = i;
            c = (b * b) % p;
            t = (t * c) % p;
            r = (r * b) % p;
        }
    }

    private static int ModPow(int baseVal, int exp, int mod)
    {
        long result = 1;
        long b = baseVal % mod;

        while (exp > 0)
        {
            if ((exp & 1) == 1)
                result = (result * b) % mod;
            exp >>= 1;
            b = (b * b) % mod;
        }

        return (int)result;
    }

    /// <summary>
    /// Injects an insight from convergence or external source.
    /// </summary>
    public async Task InjectInsightAsync(string insight, CancellationToken ct = default)
    {
        await _engine.AddFactAsync($"(insight \"{insight}\")", ct);
        await EmitThoughtAsync($"(integrated-insight {StreamId} \"{insight}\")", ThoughtAtomType.Insight, ct);
    }

    /// <summary>
    /// Emits a thought atom to the orchestrator.
    /// </summary>
    private async Task EmitThoughtAsync(string content, ThoughtAtomType type, CancellationToken ct)
    {
        var atom = new ThoughtAtom
        {
            StreamId = StreamId,
            Content = content,
            Type = type,
            Timestamp = DateTime.UtcNow,
            SequenceNumber = AtomCount++,
        };

        _recentAtoms.Enqueue(atom);
        while (_recentAtoms.Count > 50)
        {
            _recentAtoms.TryDequeue(out _);
        }

        LastActivity = DateTime.UtcNow;
        await _orchestrator.EmitAtomAsync(atom, ct);
    }

    /// <summary>
    /// Disposes the stream node.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _engine.Dispose();
        return ValueTask.CompletedTask;
    }
}