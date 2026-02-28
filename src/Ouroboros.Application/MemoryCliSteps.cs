// <copyright file="MemoryCliSteps.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.Persistence;

namespace Ouroboros.Application;

/// <summary>
/// CLI Pipeline steps for Ouroboros memory administration.
/// Enables self-management of vector collections, health checks, and memory architecture.
/// </summary>
public static partial class MemoryCliSteps
{
    private static OuroborosMemoryManager? _memoryManager;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets or initializes the memory manager singleton.
    /// </summary>
    private static async Task<OuroborosMemoryManager> GetMemoryManagerAsync(string endpoint = Configuration.DefaultEndpoints.QdrantRest)
    {
        if (_memoryManager == null)
        {
            lock (_lock)
            {
                _memoryManager ??= new OuroborosMemoryManager(endpoint);
            }
            await _memoryManager.InitializeAsync();
        }
        return _memoryManager;
    }

    /// <summary>
    /// Display the memory architecture map.
    /// Usage: MemoryMap()
    /// </summary>
    [PipelineToken("MemoryMap", "ShowMemory")]
    public static Step<CliPipelineState, CliPipelineState> MemoryMap(string? args = null)
        => async s =>
        {
            try
            {
                var manager = await GetMemoryManagerAsync();
                var map = await manager.GetMemoryMapAsync();
                Console.WriteLine(map);
                s.Context = map;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[memory] Failed to generate memory map: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Perform a health check on all memory collections.
    /// Usage: MemoryHealth()
    /// Usage: MemoryHealth('heal') - auto-heal dimension mismatches
    /// </summary>
    [PipelineToken("MemoryHealth", "CheckMemory")]
    public static Step<CliPipelineState, CliPipelineState> MemoryHealth(string? args = null)
        => async s =>
        {
            try
            {
                var autoHeal = args?.ToLowerInvariant().Contains("heal") == true;
                var manager = await GetMemoryManagerAsync();
                var report = await manager.PerformHealthCheckAsync(autoHeal);

                Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
                Console.WriteLine("║           OUROBOROS MEMORY HEALTH CHECK               ║");
                Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
                Console.WriteLine($"║  Healthy Collections:   {report.HealthyCollections,5}                        ║");
                Console.WriteLine($"║  Unhealthy Collections: {report.UnhealthyCollections,5}                        ║");
                Console.WriteLine($"║  Total Vectors:         {report.Statistics.TotalVectors,10}                 ║");
                Console.WriteLine($"║  Collection Links:      {report.Statistics.CollectionLinks,5}                        ║");

                if (report.HealedCollections.Any())
                {
                    Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
                    Console.WriteLine("║  AUTO-HEALED:                                         ║");
                    foreach (var healed in report.HealedCollections)
                    {
                        Console.WriteLine($"║    ✓ {healed,-47} ║");
                    }
                }

                if (report.RemainingIssues.Any())
                {
                    Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
                    Console.WriteLine("║  ISSUES REMAINING:                                    ║");
                    foreach (var issue in report.RemainingIssues)
                    {
                        Console.WriteLine($"║    ⚠ {issue,-47} ║");
                    }
                }

                Console.WriteLine("╚═══════════════════════════════════════════════════════╝");

                s.Context = $"Health check complete. Healthy: {report.HealthyCollections}, Unhealthy: {report.UnhealthyCollections}";
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[memory] Health check failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// List all memory collections with details.
    /// Usage: MemoryList()
    /// Usage: MemoryList('layer=procedural') - filter by memory layer
    /// </summary>
    [PipelineToken("MemoryList", "ListCollections")]
    public static Step<CliPipelineState, CliPipelineState> MemoryList(string? args = null)
        => async s =>
        {
            try
            {
                var manager = await GetMemoryManagerAsync();
                var collections = await manager.Admin.GetAllCollectionsAsync();

                // Parse layer filter
                MemoryLayer? filterLayer = null;
                if (!string.IsNullOrEmpty(args))
                {
                    var layerArg = args.ToLowerInvariant().Replace("layer=", "");
                    filterLayer = layerArg switch
                    {
                        "working" => MemoryLayer.Working,
                        "episodic" => MemoryLayer.Episodic,
                        "semantic" => MemoryLayer.Semantic,
                        "procedural" => MemoryLayer.Procedural,
                        "autobiographical" or "self" => MemoryLayer.Autobiographical,
                        _ => null
                    };
                }

                Console.WriteLine("┌────────────────────────────────────┬────────┬──────────┬────────────┐");
                Console.WriteLine("│ Collection                         │ Dim    │ Vectors  │ Layer      │");
                Console.WriteLine("├────────────────────────────────────┼────────┼──────────┼────────────┤");

                foreach (var col in collections.OrderBy(c => c.Name))
                {
                    var layer = manager.GetLayerForCollection(col.Name);

                    // Apply filter
                    if (filterLayer.HasValue && layer != filterLayer.Value) continue;

                    var layerStr = layer?.ToString() ?? "Other";
                    var status = col.Status == Qdrant.Client.Grpc.CollectionStatus.Green ? " " : "⚠";
                    Console.WriteLine($"│{status}{col.Name,-35}│ {col.VectorSize,6} │ {col.PointsCount,8} │ {layerStr,-10} │");
                }

                Console.WriteLine("└────────────────────────────────────┴────────┴──────────┴────────────┘");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[memory] Failed to list collections: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Create a new memory collection.
    /// Usage: MemoryCreate('collection_name')
    /// Usage: MemoryCreate('collection_name;dim=768')
    /// </summary>
    [PipelineToken("MemoryCreate", "CreateCollection")]
    public static Step<CliPipelineState, CliPipelineState> MemoryCreate(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[memory] Usage: MemoryCreate('collection_name') or MemoryCreate('name;dim=768')");
                return s;
            }

            try
            {
                var parts = args.Split(';');
                var name = parts[0].Trim();
                var dim = 768;

                foreach (var part in parts.Skip(1))
                {
                    if (part.StartsWith("dim=") && int.TryParse(part[4..], out var d))
                    {
                        dim = d;
                    }
                }

                var manager = await GetMemoryManagerAsync();
                var success = await manager.Admin.CreateCollectionAsync(name, dim);

                if (success)
                {
                    Console.WriteLine($"[memory] ✓ Created collection '{name}' with {dim} dimensions");
                }
                else
                {
                    Console.WriteLine($"[memory] Collection '{name}' already exists or creation failed");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[memory] Failed to create collection: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Delete a memory collection.
    /// Usage: MemoryDelete('collection_name')
    /// </summary>
    [PipelineToken("MemoryDelete", "DeleteCollection")]
    public static Step<CliPipelineState, CliPipelineState> MemoryDelete(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[memory] Usage: MemoryDelete('collection_name')");
                return s;
            }

            try
            {
                var name = args.Trim();
                var manager = await GetMemoryManagerAsync();
                var success = await manager.Admin.DeleteCollectionAsync(name);

                if (success)
                {
                    Console.WriteLine($"[memory] ✓ Deleted collection '{name}'");
                }
                else
                {
                    Console.WriteLine($"[memory] Collection '{name}' not found or deletion failed");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[memory] Failed to delete collection: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Auto-heal all collections with dimension mismatches.
    /// WARNING: This deletes data in mismatched collections!
    /// Usage: MemoryHeal()
    /// Usage: MemoryHeal('768') - heal to specific dimension
    /// </summary>
    [PipelineToken("MemoryHeal", "HealMemory")]
    public static Step<CliPipelineState, CliPipelineState> MemoryHeal(string? args = null)
        => async s =>
        {
            try
            {
                var dim = 768;
                if (!string.IsNullOrEmpty(args) && int.TryParse(args.Trim(), out var d))
                {
                    dim = d;
                }

                var manager = await GetMemoryManagerAsync();
                var healed = await manager.Admin.AutoHealDimensionMismatchesAsync(dim);

                if (healed.Any())
                {
                    Console.WriteLine($"[memory] ✓ Auto-healed {healed.Count} collections to {dim} dimensions:");
                    foreach (var col in healed)
                    {
                        Console.WriteLine($"         - {col}");
                    }
                }
                else
                {
                    Console.WriteLine("[memory] No collections needed healing");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[memory] Auto-heal failed: {ex.Message}");
            }

            return s;
        };

}
