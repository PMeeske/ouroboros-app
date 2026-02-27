// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
namespace Ouroboros.CLI.Subsystems.Autonomy;

using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Application.Configuration;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Subsystems;
using Ouroboros.Core.Configuration;
using Ouroboros.Network;
using Qdrant.Client;
using Spectre.Console;

/// <summary>
/// Manages network state tracking (Merkle-DAG + Qdrant) and the
/// persistent network state projector.
/// Extracted from <see cref="AutonomySubsystem"/> to reduce class size.
/// </summary>
internal sealed class NetworkStateManager
{
    // ── State ────────────────────────────────────────────────────────────
    public NetworkStateTracker? NetworkTracker { get; private set; }
    public PersistentNetworkStateProjector? NetworkProjector { get; private set; }

    /// <summary>
    /// Initializes the network state tracker with Qdrant or in-memory persistence.
    /// </summary>
    public async Task InitializeNetworkStateCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            NetworkTracker = new NetworkStateTracker();

            await TryConfigureQdrantPersistence(ctx);

            if (ctx.Memory.MeTTaEngine != null)
            {
                NetworkTracker.ConfigureMeTTaExport(ctx.Memory.MeTTaEngine, autoExport: true);
                AnsiConsole.MarkupLine(
                    $"    {OuroborosTheme.Ok("MeTTa symbolic export enabled (DAG facts -> MeTTa)")}");
            }

            NetworkTracker.BranchReified += (_, args) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NetworkState] Branch '{args.BranchName}' reified: {args.NodesCreated} nodes");
            };

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"  {OuroborosTheme.Warn($"NetworkState initialization failed: {ex.Message}")}");
            NetworkTracker = new NetworkStateTracker();
        }
    }

    /// <summary>
    /// Initializes the persistent network state projector for cross-session learning.
    /// </summary>
    public async Task InitializeNetworkProjectorCoreAsync(SubsystemInitContext ctx)
    {
        var embedding = ctx.Models.Embedding;
        if (embedding == null) return;

        try
        {
            var dag = new Ouroboros.Network.MerkleDag();
            Func<string, Task<float[]>> embedFunc = async text =>
                await embedding.CreateEmbeddingsAsync(text);

            var npClient = ctx.Services?.GetService<QdrantClient>();
            var npRegistry = ctx.Services?.GetService<IQdrantCollectionRegistry>();

            if (npClient != null && npRegistry != null)
            {
                NetworkProjector = new PersistentNetworkStateProjector(
                    dag, npClient, npRegistry, embedFunc);
            }
            else
            {
                var qdrantEndpoint = NormalizeEndpoint(ctx.Config.QdrantEndpoint, DefaultEndpoints.QdrantGrpc);
                NetworkProjector = new PersistentNetworkStateProjector(
                    dag, qdrantEndpoint, embedFunc);
            }

            await NetworkProjector.InitializeAsync(CancellationToken.None);
            ctx.Output.RecordInit("Network Projector", true,
                $"epoch {NetworkProjector.CurrentEpoch}, {NetworkProjector.RecentLearnings.Count} learnings");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"  {OuroborosTheme.Warn($"Network Projector: {ex.Message}")}");
        }
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        NetworkTracker?.Dispose();
        if (NetworkProjector != null)
            await NetworkProjector.DisposeAsync();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────

    private async Task TryConfigureQdrantPersistence(SubsystemInitContext ctx)
    {
        var dagClient = ctx.Services?.GetService<QdrantClient>();
        var dagRegistry = ctx.Services?.GetService<IQdrantCollectionRegistry>();
        var dagSettings = ctx.Services?.GetService<QdrantSettings>();

        if (dagClient != null && dagRegistry != null && dagSettings != null)
        {
            await TryConfigureQdrantViaDi(ctx, dagClient, dagRegistry, dagSettings);
            return;
        }

        if (!string.IsNullOrEmpty(ctx.Config.QdrantEndpoint))
        {
            await TryConfigureQdrantViaEndpoint(ctx);
            return;
        }

        ctx.Output.RecordInit("Network State", true, "Merkle-DAG (in-memory)");
    }

    private async Task TryConfigureQdrantViaDi(
        SubsystemInitContext ctx,
        QdrantClient dagClient,
        IQdrantCollectionRegistry dagRegistry,
        QdrantSettings dagSettings)
    {
        try
        {
            Func<string, Task<float[]>>? embeddingFunc = null;
            if (ctx.Models.Embedding != null)
                embeddingFunc = async text => await ctx.Models.Embedding.CreateEmbeddingsAsync(text);

            var dagStore = new Ouroboros.Network.QdrantDagStore(
                dagClient, dagRegistry, dagSettings, embeddingFunc);
            await dagStore.InitializeAsync();
            NetworkTracker!.ConfigureQdrantPersistence(dagStore, autoPersist: true);
            ctx.Output.RecordInit("Network State", true, "Merkle-DAG with Qdrant persistence (DI)");
        }
        catch (Exception qdrantEx)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NetworkState] Qdrant DAG storage unavailable: {qdrantEx.Message}");
            ctx.Output.RecordInit("Network State", true, "Merkle-DAG (in-memory)");
        }
    }

    private async Task TryConfigureQdrantViaEndpoint(SubsystemInitContext ctx)
    {
        try
        {
            Func<string, Task<float[]>>? embeddingFunc = null;
            if (ctx.Models.Embedding != null)
                embeddingFunc = async text => await ctx.Models.Embedding.CreateEmbeddingsAsync(text);

            var dagConfig = new Ouroboros.Network.QdrantDagConfig(
                Endpoint: ctx.Config.QdrantEndpoint,
                NodesCollection: "ouroboros_dag_nodes",
                EdgesCollection: "ouroboros_dag_edges",
                VectorSize: 768);
            var dagStore = new Ouroboros.Network.QdrantDagStore(dagConfig, embeddingFunc);
            await dagStore.InitializeAsync();
            NetworkTracker!.ConfigureQdrantPersistence(dagStore, autoPersist: true);
            ctx.Output.RecordInit("Network State", true, "Merkle-DAG with Qdrant persistence");
        }
        catch (Exception qdrantEx)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NetworkState] Qdrant DAG storage unavailable: {qdrantEx.Message}");
            ctx.Output.RecordInit("Network State", true, "Merkle-DAG (in-memory)");
        }
    }

    internal static string NormalizeEndpoint(string? rawEndpoint, string fallbackEndpoint)
    {
        var endpoint = (rawEndpoint ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
            return fallbackEndpoint;

        if (!endpoint.Contains("://", StringComparison.Ordinal))
            endpoint = $"http://{endpoint}";

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
            return fallbackEndpoint;

        return uri.ToString().TrimEnd('/');
    }
}
