// <copyright file="ParallelMeTTaThoughtStreams.ModuloSquare.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Numerics;
using Ouroboros.Application.Tools;

/// <summary>
/// Partial class containing modulo-square theory solving capabilities.
/// </summary>
public sealed partial class ParallelMeTTaThoughtStreams
{
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
            var match = SolutionExtractRegex().Match(derivation);

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
}
