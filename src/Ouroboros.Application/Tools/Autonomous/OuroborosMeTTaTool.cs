// <copyright file="OuroborosMeTTaTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Ouroboros.Application.Services;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Tool for self-referential Ouroboros MeTTa atom operations.
/// </summary>
public class OuroborosMeTTaTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public OuroborosMeTTaTool(IAutonomousToolContext context) => _ctx = context;
    public OuroborosMeTTaTool() : this(AutonomousTools.DefaultContext) { }

    /// <inheritdoc/>
    public string Name => "ouroboros_metta";

    /// <inheritdoc/>
    public string Description => "Create and manipulate self-referential Ouroboros MeTTa atoms. Input: JSON {\"mode\":\"create|loop|network|merge|reflect\", \"concept\":\"...\", \"iterations\":10}";

    /// <inheritdoc/>
    public string? JsonSchema => null;

    /// <summary>
    /// Shared parallel streams orchestrator. Delegates to <see cref="IAutonomousToolContext.MeTTaOrchestrator"/>.
    /// </summary>
    public static ParallelMeTTaThoughtStreams? SharedOrchestrator
    {
        get => AutonomousTools.DefaultContext.MeTTaOrchestrator;
        set => AutonomousTools.DefaultContext.MeTTaOrchestrator = value;
    }

    /// <summary>
    /// Delegate for Ollama inference. Delegates to <see cref="IAutonomousToolContext.OllamaFunction"/>.
    /// </summary>
    public static Func<string, CancellationToken, Task<string>>? OllamaFunction
    {
        get => AutonomousTools.DefaultContext.OllamaFunction;
        set => AutonomousTools.DefaultContext.OllamaFunction = value;
    }

    /// <summary>
    /// Optional callback to emit Ouroboros atom events (OnSelfConsumption, OnFixedPoint) to the
    /// cognitive thought stream. Delegates to <see cref="IAutonomousToolContext.CognitiveEmitFunc"/>.
    /// </summary>
    public static Action<string>? CognitiveEmitFunc
    {
        get => AutonomousTools.DefaultContext.CognitiveEmitFunc;
        set => AutonomousTools.DefaultContext.CognitiveEmitFunc = value;
    }

    /// <summary>
    /// Active Ouroboros atoms for persistent self-reference.
    /// </summary>
    public static List<OuroborosAtom> ActiveAtoms { get; } = [];

    /// <inheritdoc/>
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var mode = doc.RootElement.TryGetProperty("mode", out var m) ? m.GetString() ?? "create" : "create";
            var concept = doc.RootElement.TryGetProperty("concept", out var c) ? c.GetString() ?? "self" : "self";
            var iterations = doc.RootElement.TryGetProperty("iterations", out var i) ? i.GetInt32() : 10;
            var atomIndex = doc.RootElement.TryGetProperty("atom_index", out var ai) ? ai.GetInt32() : 0;

            // Ensure orchestrator exists
            var orchestrator = _ctx.MeTTaOrchestrator ?? new ParallelMeTTaThoughtStreams();
            if (_ctx.OllamaFunction != null)
            {
                orchestrator.ConnectOllama(_ctx.OllamaFunction);
            }

            var sb = new StringBuilder();
            sb.AppendLine("\ud83d\udc0d **Ouroboros MeTTa Atom**");
            sb.AppendLine();

            switch (mode.ToLowerInvariant())
            {
                case "create":
                    return await CreateOuroboros(concept, orchestrator, sb, ct);

                case "loop":
                case "strange_loop":
                    return await RunStrangeLoop(atomIndex, iterations, orchestrator, sb, ct);

                case "network":
                    return await CreateNetwork(iterations, orchestrator, sb, ct);

                case "merge":
                    return await MergeAtoms(atomIndex, orchestrator, sb);

                case "reflect":
                    return ReflectOnAtoms(atomIndex, sb);

                case "godel":
                    return await CreateGodelian(orchestrator, sb, ct);

                case "ycombinator":
                    return await ApplyYCombinator(atomIndex, iterations, sb);

                default:
                    return Result<string, string>.Failure($"Unknown mode: {mode}. Use: create, loop, network, merge, reflect, godel, ycombinator");
            }
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON input. Expected: {\"mode\":\"...\", \"concept\":\"...\"}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Ouroboros operation failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> CreateOuroboros(
        string concept,
        ParallelMeTTaThoughtStreams orchestrator,
        StringBuilder sb,
        CancellationToken ct)
    {
        var (atom, node) = await orchestrator.CreateOuroborosStreamAsync(concept);
        ActiveAtoms.Add(atom);

        // Wire atom events to the cognitive stream (if callback is set)
        if (_ctx.CognitiveEmitFunc != null)
        {
            atom.OnSelfConsumption += (a, record) =>
                _ctx.CognitiveEmitFunc!($"Ouroboros depth={a.SelfReferenceDepth}: {record[..Math.Min(80, record.Length)]}");
            atom.OnFixedPoint += (a) =>
                _ctx.CognitiveEmitFunc!($"Fixed point! emergence={a.EmergenceLevel:F3} after {a.SelfReferenceDepth} cycles");
        }

        sb.AppendLine($"**Mode:** Create Self-Aware Ouroboros");
        sb.AppendLine($"**Seed Concept:** {concept}");
        sb.AppendLine($"**Atom ID:** {atom.Id}");
        sb.AppendLine($"**Index:** {ActiveAtoms.Count - 1} (use for further operations)");
        sb.AppendLine();
        sb.AppendLine("**Initial State:**");
        sb.AppendLine($"```metta");
        sb.AppendLine(atom.Reflect());
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**MeTTa Atoms Generated:**");
        foreach (var mettaAtom in atom.ToMeTTaAtoms().Take(5))
        {
            sb.AppendLine($"  \u2022 `{mettaAtom}`");
        }
        if (atom.ToMeTTaAtoms().Count > 5)
        {
            sb.AppendLine($"  \u2022 ... and {atom.ToMeTTaAtoms().Count - 5} more");
        }

        return Result<string, string>.Success(sb.ToString());
    }

    private async Task<Result<string, string>> RunStrangeLoop(
        int atomIndex,
        int iterations,
        ParallelMeTTaThoughtStreams orchestrator,
        StringBuilder sb,
        CancellationToken ct)
    {
        if (atomIndex < 0 || atomIndex >= ActiveAtoms.Count)
        {
            if (ActiveAtoms.Count == 0)
            {
                // Create a default atom if none exist
                var (newAtom, _) = await orchestrator.CreateOuroborosStreamAsync("self");
                ActiveAtoms.Add(newAtom);
                atomIndex = 0;
            }
            else
            {
                return Result<string, string>.Failure($"Invalid atom_index. Valid range: 0-{ActiveAtoms.Count - 1}");
            }
        }

        var atom = ActiveAtoms[atomIndex];
        var startDepth = atom.SelfReferenceDepth;

        sb.AppendLine($"**Mode:** Strange Loop");
        sb.AppendLine($"**Atom:** {atom.Id.ToString()[..8]}");
        sb.AppendLine($"**Iterations:** {iterations}");
        sb.AppendLine($"**Starting Depth:** {startDepth}");
        sb.AppendLine();
        sb.AppendLine("**Self-Consumption Log:**");

        var thoughts = new List<ThoughtAtom>();
        await foreach (var thought in orchestrator.RunStrangeLoopAsync(atom, iterations, ct))
        {
            thoughts.Add(thought);
            if (thoughts.Count <= 10)
            {
                sb.AppendLine($"  [{thought.SequenceNumber}] {(thought.Content.Length > 80 ? thought.Content[..80] + "..." : thought.Content)}");
            }
        }

        if (thoughts.Count > 10)
        {
            sb.AppendLine($"  ... and {thoughts.Count - 10} more iterations");
        }

        sb.AppendLine();
        sb.AppendLine("**Final State:**");
        sb.AppendLine($"  \u2022 Depth: {atom.SelfReferenceDepth} (gained {atom.SelfReferenceDepth - startDepth})");
        sb.AppendLine($"  \u2022 Emergence Level: {atom.EmergenceLevel:F3}");
        sb.AppendLine($"  \u2022 Fixed Point Reached: {atom.IsFixedPoint}");

        if (atom.IsFixedPoint)
        {
            sb.AppendLine();
            sb.AppendLine("\ud83c\udfaf **FIXED POINT ACHIEVED!** The Ouroboros has completed its strange loop.");
        }

        return Result<string, string>.Success(sb.ToString());
    }

    private async Task<Result<string, string>> CreateNetwork(
        int count,
        ParallelMeTTaThoughtStreams orchestrator,
        StringBuilder sb,
        CancellationToken ct)
    {
        var networkAtoms = await orchestrator.CreateOuroborosNetworkAsync(Math.Max(2, Math.Min(count, 7)));

        sb.AppendLine($"**Mode:** Ouroboros Network");
        sb.AppendLine($"**Network Size:** {networkAtoms.Count}");
        sb.AppendLine();
        sb.AppendLine("**Network Nodes:**");

        foreach (var (atom, node) in networkAtoms)
        {
            ActiveAtoms.Add(atom);
            var index = ActiveAtoms.Count - 1;
            sb.AppendLine($"  \u2022 [{index}] {atom.Id.ToString()[..8]} - depth={atom.SelfReferenceDepth}, emergence={atom.EmergenceLevel:F3}");
        }

        sb.AppendLine();
        sb.AppendLine("**Network Topology:** Circular (each node aware of neighbors)");
        sb.AppendLine();
        sb.AppendLine("Use `ouroboros_metta` with `mode: loop` and `atom_index` to run strange loops on individual nodes.");

        return Result<string, string>.Success(sb.ToString());
    }

    private async Task<Result<string, string>> MergeAtoms(
        int atomIndex,
        ParallelMeTTaThoughtStreams orchestrator,
        StringBuilder sb)
    {
        if (ActiveAtoms.Count < 2)
        {
            return Result<string, string>.Failure("Need at least 2 Ouroboros atoms to merge. Create more first.");
        }

        var atom1Index = atomIndex;
        var atom2Index = (atomIndex + 1) % ActiveAtoms.Count;

        if (atom1Index < 0 || atom1Index >= ActiveAtoms.Count)
        {
            atom1Index = 0;
            atom2Index = 1;
        }

        var atom1 = ActiveAtoms[atom1Index];
        var atom2 = ActiveAtoms[atom2Index];

        var (merged, node) = await orchestrator.MergeOuroborosStreamsAsync(atom1, atom2);
        ActiveAtoms.Add(merged);

        sb.AppendLine($"**Mode:** Merge Ouroboros Atoms");
        sb.AppendLine($"**Source 1:** [{atom1Index}] {atom1.Id.ToString()[..8]}");
        sb.AppendLine($"**Source 2:** [{atom2Index}] {atom2.Id.ToString()[..8]}");
        sb.AppendLine($"**Merged:** [{ActiveAtoms.Count - 1}] {merged.Id.ToString()[..8]}");
        sb.AppendLine();
        sb.AppendLine("**Merged State:**");
        sb.AppendLine($"```metta");
        sb.AppendLine(merged.Reflect());
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine($"**Combined Emergence:** {merged.EmergenceLevel:F3}");
        sb.AppendLine($"**Transformation History:** {merged.TransformationHistory.Count} records");

        return Result<string, string>.Success(sb.ToString());
    }

    private Result<string, string> ReflectOnAtoms(int atomIndex, StringBuilder sb)
    {
        if (ActiveAtoms.Count == 0)
        {
            return Result<string, string>.Failure("No Ouroboros atoms exist. Create one first with mode: create");
        }

        sb.AppendLine($"**Mode:** Reflect on Ouroboros Atoms");
        sb.AppendLine($"**Active Atoms:** {ActiveAtoms.Count}");
        sb.AppendLine();

        if (atomIndex >= 0 && atomIndex < ActiveAtoms.Count)
        {
            // Detailed reflection on specific atom
            var atom = ActiveAtoms[atomIndex];
            sb.AppendLine($"**Detailed Reflection on [{atomIndex}]:**");
            sb.AppendLine($"```metta");
            sb.AppendLine(atom.Reflect());
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("**Transformation History:**");
            foreach (var transform in atom.TransformationHistory.TakeLast(10))
            {
                sb.AppendLine($"  \u2022 {transform}");
            }
            if (atom.TransformationHistory.Count > 10)
            {
                sb.AppendLine($"  ... and {atom.TransformationHistory.Count - 10} earlier transformations");
            }

            if (atom.Children.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"**Spawned Children:** {atom.Children.Count}");
            }
        }
        else
        {
            // Summary of all atoms
            sb.AppendLine("**All Ouroboros Atoms:**");
            for (int idx = 0; idx < ActiveAtoms.Count; idx++)
            {
                var atom = ActiveAtoms[idx];
                sb.AppendLine($"  [{idx}] {atom}");
            }
        }

        return Result<string, string>.Success(sb.ToString());
    }

    private async Task<Result<string, string>> CreateGodelian(
        ParallelMeTTaThoughtStreams orchestrator,
        StringBuilder sb,
        CancellationToken ct)
    {
        var atom = OuroborosAtomFactory.CreateGodelian();
        ActiveAtoms.Add(atom);

        sb.AppendLine($"**Mode:** G\u00f6delian Self-Reference");
        sb.AppendLine($"**Atom ID:** {atom.Id}");
        sb.AppendLine($"**Index:** {ActiveAtoms.Count - 1}");
        sb.AppendLine();
        sb.AppendLine("This Ouroboros embodies G\u00f6del's self-referential statement pattern:");
        sb.AppendLine("*\"This statement refers to itself\"*");
        sb.AppendLine();
        sb.AppendLine("**State:**");
        sb.AppendLine($"```metta");
        sb.AppendLine(atom.Reflect());
        sb.AppendLine("```");

        return Result<string, string>.Success(sb.ToString());
    }

    private async Task<Result<string, string>> ApplyYCombinator(
        int atomIndex,
        int iterations,
        StringBuilder sb)
    {
        if (atomIndex < 0 || atomIndex >= ActiveAtoms.Count)
        {
            if (ActiveAtoms.Count == 0)
            {
                var atom = new OuroborosAtom("(identity x)");
                ActiveAtoms.Add(atom);
                atomIndex = 0;
            }
            else
            {
                atomIndex = 0;
            }
        }

        var targetAtom = ActiveAtoms[atomIndex];
        var beforeCore = targetAtom.Core;

        sb.AppendLine($"**Mode:** Y-Combinator Application");
        sb.AppendLine($"**Atom:** [{atomIndex}] {targetAtom.Id.ToString()[..8]}");
        sb.AppendLine($"**Iterations:** {iterations}");
        sb.AppendLine();
        sb.AppendLine($"**Before:** `{(beforeCore.Length > 60 ? beforeCore[..60] + "..." : beforeCore)}`");

        var result = targetAtom.ApplyYCombinator(iterations);

        sb.AppendLine();
        sb.AppendLine("**Y-Combinator Applied:**");
        sb.AppendLine("Y = \u03bbf.(\u03bbx.f(x x))(\u03bbx.f(x x))");
        sb.AppendLine();
        sb.AppendLine($"**After {targetAtom.SelfReferenceDepth} recursions:**");
        sb.AppendLine($"  \u2022 Core: `{(result.Length > 80 ? result[..80] + "..." : result)}`");
        sb.AppendLine($"  \u2022 Emergence: {targetAtom.EmergenceLevel:F3}");
        sb.AppendLine($"  \u2022 Fixed Point: {targetAtom.IsFixedPoint}");

        return Result<string, string>.Success(sb.ToString());
    }
}
