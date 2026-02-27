// <copyright file="ParallelMeTTaThinkTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Ouroboros.Application.Services;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Parallel MeTTa thought streams tool for multi-theory exploration.
/// Runs multiple symbolic reasoning engines concurrently with Ollama fusion.
/// </summary>
public class ParallelMeTTaThinkTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public ParallelMeTTaThinkTool(IAutonomousToolContext context) => _ctx = context;
    public ParallelMeTTaThinkTool() : this(AutonomousTools.DefaultContext) { }

    public string Name => "parallel_metta_think";
    public string Description => "Run parallel MeTTa symbolic thought streams with Ollama fusion. Input: JSON {\"query\":\"...\", \"streams\":3, \"mode\":\"explore|solve_square|converge\", \"target\":123}";
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

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var query = doc.RootElement.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
            var streamCount = doc.RootElement.TryGetProperty("streams", out var s) ? s.GetInt32() : 3;
            var mode = doc.RootElement.TryGetProperty("mode", out var m) ? m.GetString() ?? "explore" : "explore";
            var target = doc.RootElement.TryGetProperty("target", out var t) ? t.GetInt64() : 0;

            // Create or reuse orchestrator
            var orchestrator = _ctx.MeTTaOrchestrator ?? new ParallelMeTTaThoughtStreams(streamCount);

            if (_ctx.OllamaFunction != null)
            {
                orchestrator.ConnectOllama(_ctx.OllamaFunction);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"\ud83e\udde0 **Parallel MeTTa Thought Streams** ({mode})");
            sb.AppendLine();

            switch (mode.ToLowerInvariant())
            {
                case "solve_square":
                    if (target <= 0)
                        return Result<string, string>.Failure("Target required for solve_square mode.");

                    sb.AppendLine($"**Target:** {target}");
                    sb.AppendLine("**Solving with modulo-square theory...**\n");

                    var solution = await orchestrator.SolveModuloSquareAsync(
                        new System.Numerics.BigInteger(target),
                        maxIterations: 50,
                        ct);

                    if (solution != null)
                    {
                        sb.AppendLine($"\u2705 **Solution Found!**");
                        sb.AppendLine($"  \u221a{solution.Target} = {solution.SquareRoot}");
                        sb.AppendLine($"  Derivation: {solution.Derivation}");
                        sb.AppendLine($"  Verified: {solution.IsVerified}");
                    }
                    else
                    {
                        sb.AppendLine("\u274c No solution found within iteration limit.");
                        var stats = orchestrator.GetStats();
                        sb.AppendLine($"  Explored {stats.TotalAtomsGenerated} atoms across {stats.ActiveStreams} streams.");
                    }
                    break;

                case "converge":
                    // Create streams with different seed theories
                    var theories = new Dictionary<string, List<string>>();
                    var aspects = new[] { "logical", "intuitive", "skeptical", "creative", "analytical" };

                    for (int i = 0; i < Math.Min(streamCount, aspects.Length); i++)
                    {
                        theories[$"{aspects[i]}_stream"] = new List<string>
                        {
                            $"(perspective {aspects[i]})",
                            $"(query \"{query}\")",
                            $"(approach {aspects[i]}-reasoning)",
                        };
                    }

                    await orchestrator.CreateTheoryStreamsAsync(theories);

                    var convergenceResults = new List<string>();
                    orchestrator.OnConvergence += (e) =>
                    {
                        convergenceResults.Add($"Convergence: {string.Join(", ", e.ConvergentStreams)} \u2192 {e.SharedConcept}");
                    };

                    await orchestrator.StartParallelThinkingAsync(query, ct);

                    sb.AppendLine($"**Query:** {query}");
                    sb.AppendLine($"**Streams:** {streamCount}\n");

                    if (convergenceResults.Count > 0)
                    {
                        sb.AppendLine("**Convergences:**");
                        foreach (var conv in convergenceResults)
                        {
                            sb.AppendLine($"  \u2022 {conv}");
                        }
                    }

                    // Collect thought atoms
                    var atoms = new List<ThoughtAtom>();
                    while (orchestrator.MergedStream.TryRead(out var atom))
                    {
                        atoms.Add(atom);
                    }

                    if (atoms.Count > 0)
                    {
                        sb.AppendLine("\n**Recent Thoughts:**");
                        foreach (var atom in atoms.TakeLast(10))
                        {
                            sb.AppendLine($"  [{atom.StreamId}] {atom.Content}");
                        }
                    }
                    break;

                default: // explore
                    // Simple parallel exploration
                    for (int i = 0; i < streamCount; i++)
                    {
                        await orchestrator.CreateStreamAsync($"explorer_{i}", new[]
                        {
                            $"(explorer {i})",
                            $"(goal \"{query}\")",
                        });
                    }

                    await orchestrator.StartParallelThinkingAsync(query, ct);

                    var exploreStats = orchestrator.GetStats();
                    sb.AppendLine($"**Query:** {query}");
                    sb.AppendLine($"**Active Streams:** {exploreStats.ActiveStreams}");
                    sb.AppendLine($"**Total Atoms:** {exploreStats.TotalAtomsGenerated}\n");

                    sb.AppendLine("**Stream Details:**");
                    foreach (var detail in exploreStats.StreamDetails)
                    {
                        sb.AppendLine($"  \u2022 {detail.StreamId}: {detail.AtomCount} atoms");
                    }
                    break;
            }

            // Cleanup if we created a new orchestrator
            if (_ctx.MeTTaOrchestrator == null)
            {
                await orchestrator.DisposeAsync();
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Parallel MeTTa thinking failed: {ex.Message}");
        }
    }
}
