// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Services;

using LangChain.Providers.Ollama;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.NeuralSymbolic;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Sovereignty;
using Ouroboros.Core.CognitivePhysics;
using Ouroboros.Core.Ethics;
using CausalReasoning = Ouroboros.Core.Reasoning;
using Ouroboros.Pipeline.Memory;
using Ouroboros.Providers;
using Ouroboros.Tools.MeTTa;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;

/// <summary>
/// Consolidates duplicated initialization code shared by ImmersiveMode, RoomMode,
/// and OuroborosAgent. Each method is a single source of truth for creating a subsystem
/// that was previously copy-pasted across modes.
///
/// This is the "novel approach" — instead of each mode owning isolated instances,
/// <see cref="SharedAgentBootstrap"/> produces shared-ready instances that can be
/// consumed by any mode or handler.
/// </summary>
public static class SharedAgentBootstrap
{
    /// <summary>
    /// Creates an embedding model backed by Ollama.
    /// Previously duplicated in ImmersiveMode.RunAsync, RoomMode.RunAsync, and ModelSubsystem.
    /// </summary>
    public static IEmbeddingModel? CreateEmbeddingModel(
        string endpoint, string embedModel, Action<string>? log = null)
    {
        try
        {
            var provider = new OllamaProvider(endpoint);
            var ollamaEmbed = new OllamaEmbeddingModel(provider, embedModel);
            var model = new OllamaEmbeddingAdapter(ollamaEmbed);
            log?.Invoke("Memory systems online");
            return model;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Memory unavailable: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates an episodic memory engine backed by Qdrant.
    /// Previously duplicated in ImmersiveMode.RunAsync (line 394), RoomMode.RunAsync (line 211),
    /// and ChatSubsystem.
    /// </summary>
    public static IEpisodicMemoryEngine? CreateEpisodicMemory(
        string qdrantEndpoint,
        IEmbeddingModel? embeddingModel,
        string collectionName = "ouroboros_episodes")
    {
        if (embeddingModel == null) return null;

        try
        {
            return new EpisodicMemoryEngine(qdrantEndpoint, embeddingModel, collectionName);
        }
        catch
        {
            return null; // Qdrant unavailable
        }
    }

    /// <summary>
    /// Creates a neural-symbolic bridge backed by MeTTa.
    /// Previously duplicated in ImmersiveMode.RunAsync (line 404), RoomMode.RunAsync (line 222),
    /// and ChatSubsystem.
    /// </summary>
    public static INeuralSymbolicBridge? CreateNeuralSymbolicBridge(
        IChatCompletionModel? chatModel,
        IMeTTaEngine mettaEngine)
    {
        if (chatModel == null) return null;

        try
        {
            var kb = new SymbolicKnowledgeBase(mettaEngine);
            return new NeuralSymbolicBridge(chatModel, kb);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates cognitive physics engine and initial state.
    /// Previously duplicated in ImmersiveMode.RunAsync (line 380), RoomMode.RunAsync (line 203).
    /// </summary>
    public static (CognitivePhysicsEngine Engine, CognitiveState State) CreateCognitivePhysics()
    {
#pragma warning disable CS0618 // Obsolete IEmbeddingProvider/IEthicsGate — CPE requires them
        var engine = new CognitivePhysicsEngine(
            new Ouroboros.ApiHost.NullEmbeddingProvider(),
            new PermissiveEthicsGate());
#pragma warning restore CS0618
        var state = CognitiveState.Create("general");
        return (engine, state);
    }

    /// <summary>
    /// Creates the curiosity engine and sovereignty gate, and starts the background
    /// exploration loop that injects topics into the autonomous mind.
    /// Previously duplicated in ImmersiveMode.RunAsync (line 415), RoomMode.RunAsync (line 230).
    /// </summary>
    public static (ICuriosityEngine? Curiosity, PersonaSovereigntyGate? Sovereignty) CreateCuriosityAndSovereignty(
        IChatCompletionModel chatModel,
        IEmbeddingModel? embeddingModel,
        IMeTTaEngine mettaEngine,
        AutonomousMind? mind,
        CancellationToken ct)
    {
        ICuriosityEngine? curiosity = null;
        PersonaSovereigntyGate? sovereignty = null;

        try
        {
            var ethics = EthicsFrameworkFactory.CreateDefault();
            var memStore = new MemoryStore(embeddingModel);
            var safetyGuard = new SafetyGuard(PermissionLevel.Read, mettaEngine);
            var skills = new SkillRegistry();
            curiosity = new CuriosityEngine(chatModel, memStore, skills, safetyGuard, ethics);

            try { sovereignty = new PersonaSovereigntyGate(chatModel); }
            catch { /* sovereignty gate optional */ }

            if (mind != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(90), ct).ConfigureAwait(false);
                            if (await curiosity.ShouldExploreAsync(ct: ct).ConfigureAwait(false))
                            {
                                var opps = await curiosity
                                    .IdentifyExplorationOpportunitiesAsync(2, ct)
                                    .ConfigureAwait(false);
                                foreach (var opp in opps)
                                {
                                    if (sovereignty != null)
                                    {
                                        var verdict = await sovereignty
                                            .EvaluateExplorationAsync(opp.Description, ct)
                                            .ConfigureAwait(false);
                                        if (!verdict.Approved) continue;
                                    }
                                    mind.InjectTopic(opp.Description);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // exploration loop ended (cancellation or transient failure)
                    }
                }, ct);
            }
        }
        catch
        {
            // curiosity system optional
        }

        return (curiosity, sovereignty);
    }

    /// <summary>
    /// Extracts causal terms from natural-language input using regex patterns.
    /// Previously duplicated in ImmersiveMode.cs, ImmersiveMode.Response.cs,
    /// RoomMode.Speech.cs, RoomMode.Interjection.cs, and ChatSubsystem.cs.
    /// </summary>
    public static (string Cause, string Effect)? TryExtractCausalTerms(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var m = System.Text.RegularExpressions.Regex.Match(
            input, @"\bwhy\s+(?:does|is|did|do|are)\s+(.+?)(?:\?|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
            return ("external factors", m.Groups[1].Value.Trim().TrimEnd('?'));

        m = System.Text.RegularExpressions.Regex.Match(
            input, @"\bwhat\s+(?:causes?|leads?\s+to|results?\s+in)\s+(.+?)(?:\?|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
            return ("preceding conditions", m.Groups[1].Value.Trim().TrimEnd('?'));

        m = System.Text.RegularExpressions.Regex.Match(
            input, @"\bif\s+(.+?)\s+then\s+(.+?)(?:\?|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
            return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim().TrimEnd('?'));

        m = System.Text.RegularExpressions.Regex.Match(
            input, @"(.+?)\s+causes?\s+(.+?)(?:\?|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
            return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim().TrimEnd('?'));

        return null;
    }

    /// <summary>
    /// Builds a minimal two-node causal graph from cause and effect terms.
    /// Previously duplicated alongside <see cref="TryExtractCausalTerms"/> in 5 locations.
    /// </summary>
    public static CausalReasoning.CausalGraph BuildMinimalCausalGraph(string cause, string effect)
    {
        var causeVar  = new CausalReasoning.Variable(cause,  CausalReasoning.VariableType.Continuous, []);
        var effectVar = new CausalReasoning.Variable(effect, CausalReasoning.VariableType.Continuous, []);
        var edge      = new CausalReasoning.CausalEdge(cause, effect, 0.8, CausalReasoning.EdgeType.Direct);
        return new CausalReasoning.CausalGraph(
            [causeVar, effectVar], [edge],
            new Dictionary<string, CausalReasoning.StructuralEquation>());
    }
}
