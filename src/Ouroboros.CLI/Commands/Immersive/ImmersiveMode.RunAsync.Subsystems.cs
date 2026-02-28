// <copyright file="ImmersiveMode.RunAsync.Subsystems.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.Application.Configuration;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Core.Configuration;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Domain;
using Ouroboros.Domain.DistinctionLearning;
using Ouroboros.Network;
using Ouroboros.Options;
using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client;
using Spectre.Console;

public sealed partial class ImmersiveMode
{
    /// <summary>
    /// Initializes persistent conversation memory and network state projector.
    /// </summary>
    private async Task InitializeMemorySubsystemsAsync(
        IVoiceOptions options,
        IEmbeddingModel? embeddingModel,
        string personaName,
        CancellationToken ct)
    {
        // Initialize persistent conversation memory
        var qdrantEndpoint = NormalizeEndpoint(options.QdrantEndpoint, DefaultEndpoints.QdrantGrpc);
        {
            var client = _serviceProvider?.GetService<QdrantClient>();
            var registry = _serviceProvider?.GetService<IQdrantCollectionRegistry>();
            if (client != null && registry != null)
            {
                _conversationMemory = new PersistentConversationMemory(client, registry, embeddingModel);
            }
            else
            {
#pragma warning disable CS0618 // Obsolete endpoint-string constructor
                _conversationMemory = new PersistentConversationMemory(
                    embeddingModel,
                    new ConversationMemoryConfig { QdrantEndpoint = qdrantEndpoint });
#pragma warning restore CS0618
            }
        }
        await _conversationMemory.InitializeAsync(personaName, ct);
        var memStats = _conversationMemory.GetStats();
        if (memStats.TotalSessions > 0)
        {
            AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape($"  [Memory] Loaded {memStats.TotalSessions} previous conversations ({memStats.TotalTurns} turns)")}[/]");
        }

        // Initialize persistent network state projector for learning persistence
        if (embeddingModel != null)
        {
            try
            {
                var dag = new MerkleDag();
                var nsClient = _serviceProvider?.GetService<QdrantClient>();
                var nsRegistry = _serviceProvider?.GetService<IQdrantCollectionRegistry>();
                var nsSettings = _serviceProvider?.GetService<QdrantSettings>();
                if (nsClient != null && nsRegistry != null)
                {
                    _networkStateProjector = new PersistentNetworkStateProjector(
                        dag,
                        nsClient,
                        nsRegistry,
                        async text => await embeddingModel.CreateEmbeddingsAsync(text));
                }
                else
                {
                    _networkStateProjector = new PersistentNetworkStateProjector(
                        dag,
                        qdrantEndpoint,
                        async text => await embeddingModel.CreateEmbeddingsAsync(text));
                }
                await _networkStateProjector.InitializeAsync(ct);
                AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape($"  [NetworkState] Epoch {_networkStateProjector.CurrentEpoch}, {_networkStateProjector.RecentLearnings.Count} learnings loaded")}[/]");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [!] Network state persistence unavailable: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Initializes distinction learning, self-persistence, ethics, cognitive physics,
    /// episodic memory, causal reasoning, neural-symbolic bridge, curiosity engine,
    /// and sovereignty gate.
    /// </summary>
    private async Task InitializeCognitiveSubsystemsAsync(
        IVoiceOptions options,
        IEmbeddingModel? embeddingModel,
        InMemoryMeTTaEngine mettaEngine,
        CancellationToken ct)
    {
        var qdrantEndpoint = NormalizeEndpoint(options.QdrantEndpoint, DefaultEndpoints.QdrantGrpc);

        // Initialize distinction learning
        try
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Initializing distinction learning..."));
            var storageConfig = DistinctionStorageConfig.Default;
            var storage = new FileSystemDistinctionWeightStorage(storageConfig);
            _distinctionLearner = new DistinctionLearner(storage);
            _dream = new ConsciousnessDream();
            _currentDistinctionState = DistinctionState.Initial();
            AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [DistinctionLearning] Ready to learn from consciousness cycles")}[/]");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [!] Distinction learning unavailable: {ex.Message}"));
        }

        // Initialize self-persistence for mind state storage in Qdrant
        if (embeddingModel != null)
        {
            try
            {
                var spSettings = _serviceProvider?.GetService<QdrantSettings>();
                if (spSettings != null)
                {
                    _selfPersistence = new SelfPersistence(
                        spSettings,
                        async text => await embeddingModel.CreateEmbeddingsAsync(text));
                }
                else
                {
                    _selfPersistence = new SelfPersistence(
                        qdrantEndpoint,
                        async text => await embeddingModel.CreateEmbeddingsAsync(text));
                }
                await _selfPersistence.InitializeAsync(ct);
                AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [SelfPersistence] Qdrant collection 'ouroboros_self' ready for mind state storage")}[/]");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [!] Self-persistence unavailable: {ex.Message}"));
            }
        }

        // Initialize autonomous mind for background thinking and curiosity
        ConfigureAutonomousMind();
        WireLimitationBustingTools();
        SubscribeAutonomousMindEvents();

        // Start autonomous thinking
        _autonomousMind!.Start();
        AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Autonomous mind active (thinking, exploring, learning in background)"));
        AnsiConsole.MarkupLine(OuroborosTheme.Ok("       State persistence enabled (thoughts, emotions, learnings)"));

        // Wire up self-persistence tools to access the mind and persistence service
        if (_selfPersistence != null && _autonomousMind != null)
        {
            SystemAccessTools.SharedPersistence = _selfPersistence;
            SystemAccessTools.SharedMind = _autonomousMind;
            AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [Tools] Self-persistence tools linked: persist_self, restore_self, search_my_thoughts, persistence_stats")}[/]");
        }

        // Initialize ethics + cognitive physics + phi for response gating (via SharedAgentBootstrap)
        _immersiveEthics = Ouroboros.Core.Ethics.EthicsFrameworkFactory.CreateDefault();
        var (cogPhysicsEngine, cogState) = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateCognitivePhysics();
        _immersiveCogPhysics = cogPhysicsEngine;
        _immersiveCogState = cogState;
        _immersivePhiCalc = new Ouroboros.Providers.IITPhiCalculator();
        _immersiveLastTopic = "general";
        AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [OK] Ethics gate + CognitivePhysics + Φ (IIT) online")}[/]");

        // Episodic memory + causal reasoning (resolved from DI — registered in RegisterEngineInterfaces)
        _episodicMemory = _serviceProvider?.GetService<Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine>()
            ?? Ouroboros.CLI.Services.SharedAgentBootstrap.CreateEpisodicMemory(qdrantEndpoint, embeddingModel);
        _causalReasoning = _serviceProvider?.GetService<Ouroboros.Core.Reasoning.ICausalReasoningEngine>()
            ?? new Ouroboros.Core.Reasoning.CausalReasoningEngine();

        // Neural-symbolic bridge (via SharedAgentBootstrap)
        _neuralSymbolicBridge = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateNeuralSymbolicBridge(
            _baseModel, mettaEngine);

        // Curiosity engine + sovereignty gate (via SharedAgentBootstrap)
        (_curiosityEngine, _sovereigntyGate) = Ouroboros.CLI.Services.SharedAgentBootstrap
            .CreateCuriosityAndSovereignty(
                _baseModel!,
                embeddingModel,
                mettaEngine,
                _autonomousMind,
                ct);

        AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [OK] EpisodicMemory + NeuralSymbolic + CuriosityEngine + SovereigntyGate online")}[/]");
    }
}
