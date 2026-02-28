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
        var qdrantEndpoint = NormalizeEndpoint(options.QdrantEndpoint, Ouroboros.Core.Configuration.DefaultEndpoints.QdrantGrpc);
        {
            var client = _serviceProvider?.GetService<QdrantClient>();
            var registry = _serviceProvider?.GetService<IQdrantCollectionRegistry>();
            if (client != null && registry != null)
            {
                _tools.ConversationMemory = new PersistentConversationMemory(client, registry, embeddingModel);
            }
            else
            {
                // Create a QdrantClient from the endpoint string to use the non-obsolete constructor
                QdrantClient? fallbackClient = null;
                try
                {
                    var uri = new Uri(qdrantEndpoint);
                    fallbackClient = new QdrantClient(uri.Host, uri.Port > 0 ? uri.Port : 6334, uri.Scheme == "https");
                }
                catch
                {
                    // Qdrant not available, continue without semantic memory
                }

                if (fallbackClient != null)
                {
                    var fallbackRegistry = new Ouroboros.Domain.Vectors.QdrantCollectionRegistry(fallbackClient);
                    _tools.ConversationMemory = new PersistentConversationMemory(
                        fallbackClient,
                        fallbackRegistry,
                        embeddingModel,
                        new ConversationMemoryConfig { QdrantEndpoint = qdrantEndpoint });
                }
                else
                {
                    _tools.ConversationMemory = new PersistentConversationMemory(embeddingModel);
                }
            }
        }
        await _tools.ConversationMemory.InitializeAsync(personaName, ct);
        var memStats = _tools.ConversationMemory.GetStats();
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
                    _tools.NetworkStateProjector = new PersistentNetworkStateProjector(
                        dag,
                        nsClient,
                        nsRegistry,
                        async (text, ct2) => await embeddingModel.CreateEmbeddingsAsync(text, ct2));
                }
                else
                {
                    _tools.NetworkStateProjector = new PersistentNetworkStateProjector(
                        dag,
                        qdrantEndpoint,
                        async (text, ct2) => await embeddingModel.CreateEmbeddingsAsync(text, ct2));
                }
                await _tools.NetworkStateProjector.InitializeAsync(ct);
                AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape($"  [NetworkState] Epoch {_tools.NetworkStateProjector.CurrentEpoch}, {_tools.NetworkStateProjector.RecentLearnings.Count} learnings loaded")}[/]");
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
        var qdrantEndpoint = NormalizeEndpoint(options.QdrantEndpoint, Ouroboros.Core.Configuration.DefaultEndpoints.QdrantGrpc);

        // Initialize distinction learning
        try
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Initializing distinction learning..."));
            var storageConfig = DistinctionStorageConfig.Default;
            var storage = new FileSystemDistinctionWeightStorage(storageConfig);
            _learning.DistinctionLearner = new DistinctionLearner(storage);
            _learning.Dream = new ConsciousnessDream();
            _learning.CurrentDistinctionState = DistinctionState.Initial();
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
        if (_autonomousMind != null)
            _autonomousMind.Start();
        else
            System.Diagnostics.Debug.WriteLine("Warning: Autonomous mind not initialized — skipping Start().");
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
        _cognitive.Ethics = Ouroboros.Core.Ethics.EthicsFrameworkFactory.CreateDefault();
        var (cogPhysicsEngine, cogState) = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateCognitivePhysics();
        _cognitive.CogPhysics = cogPhysicsEngine;
        _cognitive.CogState = cogState;
        _cognitive.PhiCalc = new Ouroboros.Providers.IITPhiCalculator();
        _cognitive.LastTopic = "general";
        AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [OK] Ethics gate + CognitivePhysics + Φ (IIT) online")}[/]");

        // Episodic memory + causal reasoning (resolved from DI — registered in RegisterEngineInterfaces)
        _cognitive.EpisodicMemory = _serviceProvider?.GetService<Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine>()
            ?? Ouroboros.CLI.Services.SharedAgentBootstrap.CreateEpisodicMemory(qdrantEndpoint, embeddingModel);
        _cognitive.CausalReasoning = _serviceProvider?.GetService<Ouroboros.Core.Reasoning.ICausalReasoningEngine>()
            ?? new Ouroboros.Core.Reasoning.CausalReasoningEngine();

        // Neural-symbolic bridge (via SharedAgentBootstrap)
        _cognitive.NeuralSymbolicBridge = Ouroboros.CLI.Services.SharedAgentBootstrap.CreateNeuralSymbolicBridge(
            _learning.BaseModel, mettaEngine);

        // Curiosity engine + sovereignty gate (via SharedAgentBootstrap)
        (_cognitive.CuriosityEngine, _learning.SovereigntyGate) = Ouroboros.CLI.Services.SharedAgentBootstrap
            .CreateCuriosityAndSovereignty(
                _learning.BaseModel!,
                embeddingModel,
                mettaEngine,
                _autonomousMind,
                ct);

        AnsiConsole.MarkupLine($"[rgb(148,103,189)]{Markup.Escape("  [OK] EpisodicMemory + NeuralSymbolic + CuriosityEngine + SovereigntyGate online")}[/]");
    }
}
