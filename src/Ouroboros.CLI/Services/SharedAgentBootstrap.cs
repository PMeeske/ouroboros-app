// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Services;

using LangChain.Providers.Ollama;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Application.Extensions;
using Ouroboros.Agent.NeuralSymbolic;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Sovereignty;
using Ouroboros.Core.CognitivePhysics;
using Ouroboros.Core.Ethics;
using CausalReasoning = Ouroboros.Core.Reasoning;
using Ouroboros.Pipeline.Memory;
using Ouroboros.Providers;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;
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
public static partial class SharedAgentBootstrap
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
        catch (Grpc.Core.RpcException)
        {
            return null; // Qdrant unavailable
        }
        catch (HttpRequestException)
        {
            return null; // Qdrant unavailable (HTTP mode)
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
        catch (InvalidOperationException)
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
            catch (InvalidOperationException) { /* sovereignty gate optional */ }

            if (mind != null)
            {
                Task.Run(async () =>
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
                    catch (OperationCanceledException)
                    {
                        // exploration loop ended (cancellation)
                    }
                    catch (HttpRequestException)
                    {
                        // exploration loop ended (transient failure)
                    }
                }, ct)
                .ObserveExceptions("CuriosityEngine exploration");
            }
        }
        catch (InvalidOperationException)
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

        var m = WhyCausalRegex().Match(input);
        if (m.Success)
            return ("external factors", m.Groups[1].Value.Trim().TrimEnd('?'));

        m = WhatCausesRegex().Match(input);
        if (m.Success)
            return ("preceding conditions", m.Groups[1].Value.Trim().TrimEnd('?'));

        m = IfThenRegex().Match(input);
        if (m.Success)
            return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim().TrimEnd('?'));

        m = CausesRegex().Match(input);
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

    /// <summary>
    /// Creates a TTS service using the best available backend.
    /// Priority: Azure Neural TTS → Local Windows SAPI → OpenAI TTS → null.
    /// Previously duplicated across ImmersiveMode.Speech.cs and RoomMode.RunAsync.
    /// </summary>
    public static ITextToSpeechService? CreateTtsService(
        string? azureSpeechKey,
        string azureSpeechRegion,
        string personaName,
        string ttsVoice,
        bool preferLocal,
        Action<string>? log = null)
    {
        // Azure Neural TTS takes first priority when configured
        if (!string.IsNullOrEmpty(azureSpeechKey))
        {
            try
            {
                var tts = new AzureNeuralTtsService(azureSpeechKey, azureSpeechRegion, personaName);
                log?.Invoke($"Voice output: Azure Neural TTS ({ttsVoice})");
                return tts;
            }
            catch (Exception ex)
            {
                log?.Invoke($"Azure TTS unavailable: {ex.Message}");
            }
        }

        // Local Windows SAPI
        if (preferLocal && LocalWindowsTtsService.IsAvailable())
        {
            try
            {
                var tts = new LocalWindowsTtsService(rate: 1, volume: 100, useEnhancedProsody: true);
                log?.Invoke("Voice output: Windows SAPI");
                return tts;
            }
            catch (InvalidOperationException) { }
        }

        // OpenAI TTS fallback
        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(openAiKey))
        {
            try
            {
                var tts = new OpenAiTextToSpeechService(openAiKey);
                log?.Invoke("Voice output: OpenAI TTS");
                return tts;
            }
            catch (HttpRequestException) { /* OpenAI TTS unavailable */ }
        }

        log?.Invoke("Voice output: Text only (no TTS backend available)");
        return null;
    }

    /// <summary>
    /// Creates an STT service using the best available backend.
    /// Currently supports Whisper.net (local, no API key needed).
    /// Previously duplicated in ImmersiveMode.Speech.cs and RoomMode.Speech.cs.
    /// </summary>
    public static async Task<ISpeechToTextService?> CreateSttService(
        string? azureKey = null,
        string azureRegion = "eastus",
        Action<string>? log = null)
    {
        // Try Whisper.net (local, no API key needed)
        try
        {
            var whisper = WhisperNetService.FromModelSize("base");
            if (await whisper.IsAvailableAsync())
            {
                log?.Invoke("Voice input: Whisper.net (local)");
                return whisper;
            }
        }
        catch (InvalidOperationException) { /* Whisper.net unavailable */ }

        log?.Invoke("Voice input: No backend available (install Whisper.net for voice input)");
        return null;
    }

    /// <summary>
    /// Creates speech detection adapter for voice-activity detection.
    /// Used alongside STT for interactive voice input.
    /// </summary>
    public static AdaptiveSpeechDetector CreateSpeechDetector()
    {
        return new AdaptiveSpeechDetector(new AdaptiveSpeechDetector.SpeechDetectionConfig(
            InitialThreshold: 0.03,
            SpeechOnsetFrames: 2,
            SpeechOffsetFrames: 6,
            AdaptationRate: 0.015,
            SpeechToNoiseRatio: 2.0));
    }

    /// <summary>
    /// Creates and awakens an <see cref="ImmersivePersona"/> with its required
    /// MeTTa engine and embedding model already wired.
    /// Previously duplicated across ImmersiveMode.RunAsync and RoomMode.RunAsync.
    /// </summary>
    public static async Task<ImmersivePersona> CreateAndAwakenPersonaAsync(
        string personaName,
        IMeTTaEngine mettaEngine,
        IEmbeddingModel? embeddingModel,
        string qdrantEndpoint,
        CancellationToken ct,
        Action<string>? log = null)
    {
        log?.Invoke("Awakening persona...");
        var persona = new ImmersivePersona(personaName, mettaEngine, embeddingModel, qdrantEndpoint);
        await persona.AwakenAsync(ct);
        log?.Invoke($"{personaName} is awake");
        return persona;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\bwhy\s+(?:does|is|did|do|are)\s+(.+?)(?:\?|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex WhyCausalRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"\bwhat\s+(?:causes?|leads?\s+to|results?\s+in)\s+(.+?)(?:\?|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex WhatCausesRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"\bif\s+(.+?)\s+then\s+(.+?)(?:\?|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex IfThenRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"(.+?)\s+causes?\s+(.+?)(?:\?|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex CausesRegex();
}
