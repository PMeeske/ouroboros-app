// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Agent.MetaAI;
using Ouroboros.Application.SelfAssembly;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Resources;
using Ouroboros.Domain.Autonomous;

/// <summary>
/// Self-assembly subsystem: LLM-based neuron code generation, interactive approval flow,
/// assembly event handling, capability-gap analysis, and neuron instantiation.
/// </summary>
public sealed class SelfAssemblySubsystem : ISelfAssemblySubsystem
{
    public string Name => "SelfAssembly";
    public bool IsInitialized { get; private set; }

    private SelfAssemblyEngine? _engine;
    private BlueprintAnalyzer? _blueprintAnalyzer;
    private AutonomousCoordinator? _coordinator;

    /// <summary>Set by agent after Models are initialized (needed for code generation).</summary>
    internal ToolAwareChatModel? Llm { get; set; }

    /// <summary>Set by agent after Memory is initialized (for logging assembled neurons).</summary>
    internal List<string>? ConversationHistory { get; set; }

    public Task InitializeAsync(SubsystemInitContext ctx)
    {
        _engine = ctx.Autonomy.SelfAssemblyEngine;
        _blueprintAnalyzer = ctx.Autonomy.BlueprintAnalyzer;
        _coordinator = ctx.Autonomy.Coordinator;
        IsInitialized = true;
        ctx.Output.RecordInit("SelfAssembly", _engine != null, _engine != null ? "engine ready" : "disabled");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers code generator, approval callback, and event handlers on the engine.
    /// Call from agent's WireCrossSubsystemDependencies after Llm and ConversationHistory are set.
    /// </summary>
    internal void WireCallbacks()
    {
        if (_engine == null) return;

        if (Llm != null)
        {
            _engine.SetCodeGenerator(blueprint => GenerateNeuronCodeAsync(blueprint));
        }

        _engine.SetApprovalCallback(proposal => RequestSelfAssemblyApprovalAsync(proposal));
        _engine.NeuronAssembled += OnNeuronAssembled;
        _engine.AssemblyFailed += OnAssemblyFailed;
    }

    public async Task<IReadOnlyList<NeuronBlueprint>> AnalyzeAndProposeNeuronsAsync(CancellationToken ct = default)
    {
        if (_blueprintAnalyzer == null || _engine == null)
            return [];

        try
        {
            var recentMessages = new List<NeuronMessage>();
            var gaps = await _blueprintAnalyzer.AnalyzeGapsAsync(recentMessages, ct);
            var blueprints = new List<NeuronBlueprint>();

            foreach (var gap in gaps.Where(g => g.Importance >= 0.6))
            {
                var blueprint = await _blueprintAnalyzer.GenerateBlueprintForGapAsync(gap, ct);
                if (blueprint != null)
                    blueprints.Add(blueprint);
            }

            return blueprints;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SelfAssembly] Analysis failed: {ex.Message}");
            return [];
        }
    }

    public async Task<Neuron?> AssembleNeuronAsync(NeuronBlueprint blueprint, CancellationToken ct = default)
    {
        if (_engine == null)
            throw new InvalidOperationException("Self-assembly engine not initialized");

        var proposalResult = await _engine.SubmitBlueprintAsync(blueprint);
        if (!proposalResult.IsSuccess)
            return null;

        await Task.Delay(100, ct);

        var neurons = _engine.GetAssembledNeurons();
        if (neurons.TryGetValue(blueprint.Name, out _))
        {
            var instance = _engine.CreateNeuronInstance(blueprint.Name);
            return instance.IsSuccess ? instance.Value : null;
        }

        return null;
    }

    private async Task<string> GenerateNeuronCodeAsync(NeuronBlueprint blueprint)
    {
        if (Llm == null)
            throw new InvalidOperationException("LLM not available for code generation");

        var prompt = PromptResources.NeuronCodeGen(
            blueprint.Name,
            blueprint.Description,
            blueprint.Rationale,
            blueprint.Type.ToString(),
            string.Join(", ", blueprint.SubscribedTopics),
            string.Join(", ", blueprint.Capabilities),
            string.Join("\n", blueprint.MessageHandlers.Select(h =>
                $"- Topic '{h.TopicPattern}': {h.HandlingLogic} (responds={h.SendsResponse}, broadcasts={h.BroadcastsResult})")),
            blueprint.HasAutonomousTick
                ? $"AUTONOMOUS TICK: {blueprint.TickBehaviorDescription}"
                : "No autonomous tick behavior");

        var response = await Llm.InnerModel.GenerateTextAsync(prompt, CancellationToken.None);

        var code = ExtractCodeBlock(response);

        if (!code.Contains("using Ouroboros.Domain.Autonomous"))
            code = "using System;\nusing System.Collections.Generic;\nusing System.Threading;\nusing System.Threading.Tasks;\nusing Ouroboros.Domain.Autonomous;\n\n" + code;

        return code;
    }

    private static string ExtractCodeBlock(string response)
    {
        if (response.Contains("```csharp"))
        {
            var start = response.IndexOf("```csharp") + 9;
            var end = response.IndexOf("```", start);
            if (end > start) return response[start..end].Trim();
        }
        else if (response.Contains("```"))
        {
            var start = response.IndexOf("```") + 3;
            var end = response.IndexOf("```", start);
            if (end > start) return response[start..end].Trim();
        }

        return response;
    }

    private static async Task<bool> RequestSelfAssemblyApprovalAsync(AssemblyProposal proposal)
    {
        var blueprint = proposal.Blueprint;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           🧬 SELF-ASSEMBLY PROPOSAL                           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();

        Console.WriteLine($"\n  Neuron: {blueprint.Name}");
        Console.WriteLine($"  Description: {blueprint.Description}");
        Console.WriteLine($"  Rationale: {blueprint.Rationale}");
        Console.WriteLine($"  Type: {blueprint.Type}");
        Console.WriteLine($"  Topics: {string.Join(", ", blueprint.SubscribedTopics)}");
        Console.WriteLine($"  Capabilities: {string.Join(", ", blueprint.Capabilities)}");
        Console.WriteLine($"  Confidence: {blueprint.ConfidenceScore:P0}");

        Console.ForegroundColor = proposal.Validation.SafetyScore >= 0.8
            ? ConsoleColor.Green
            : ConsoleColor.Yellow;
        Console.WriteLine($"  Safety Score: {proposal.Validation.SafetyScore:P0}");
        Console.ResetColor();

        if (proposal.Validation.Violations.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  Violations: {string.Join(", ", proposal.Validation.Violations)}");
            Console.ResetColor();
        }

        if (proposal.Validation.Warnings.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  Warnings: {string.Join(", ", proposal.Validation.Warnings)}");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.Write("  Approve this self-assembly? [y/N]: ");

        var response = await Task.Run(Console.ReadLine);
        return response?.Trim().ToLowerInvariant() is "y" or "yes";
    }

    private void OnNeuronAssembled(object? sender, NeuronAssembledEvent e)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  🧬 SELF-ASSEMBLED: {e.NeuronName} (Type: {e.NeuronType.Name})");
        Console.ResetColor();

        if (_engine is not null)
        {
            var instanceResult = _engine.CreateNeuronInstance(e.NeuronName);
            if (instanceResult.IsSuccess && instanceResult.Value is Neuron neuron)
            {
                _coordinator?.Network?.RegisterNeuron(neuron);
                neuron.Start();
            }
        }

        ConversationHistory?.Add($"[SYSTEM] Self-assembled neuron: {e.NeuronName}");
    }

    private static void OnAssemblyFailed(object? sender, AssemblyFailedEvent e)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ⚠ Assembly failed for '{e.NeuronName}': {e.Reason}");
        Console.ResetColor();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
