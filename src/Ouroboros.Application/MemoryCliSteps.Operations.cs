// <copyright file="MemoryCliSteps.Operations.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.Persistence;

namespace Ouroboros.Application;

/// <summary>
/// Additional CLI pipeline steps for memory administration:
/// linking, statistics, snapshots, and clearing memory layers.
/// </summary>
public static partial class MemoryCliSteps
{
    /// <summary>
    /// Link two collections in the memory graph.
    /// Usage: MemoryLink('source;target;relation_type')
    /// Example: MemoryLink('ouroboros_skills;tools;depends_on')
    /// </summary>
    [PipelineToken("MemoryLink", "LinkCollections")]
    public static Step<CliPipelineState, CliPipelineState> MemoryLink(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[memory] Usage: MemoryLink('source;target;relation_type')");
                Console.WriteLine("[memory] Relation types: depends_on, indexes, extends, mirrors, aggregates, part_of, related_to");
                return s;
            }

            try
            {
                var parts = args.Split(';');
                if (parts.Length < 3)
                {
                    Console.WriteLine("[memory] Invalid format. Use: MemoryLink('source;target;relation_type')");
                    return s;
                }

                var source = parts[0].Trim();
                var target = parts[1].Trim();
                var relation = parts[2].Trim();
                var description = parts.Length > 3 ? parts[3].Trim() : null;

                var manager = await GetMemoryManagerAsync();
                manager.LinkCollections(source, target, relation, description);

                Console.WriteLine($"[memory] ✓ Linked {source} ─{relation}→ {target}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[memory] Failed to link collections: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Show links for a collection.
    /// Usage: MemoryLinks('collection_name')
    /// </summary>
    [PipelineToken("MemoryLinks", "ShowLinks")]
    public static Step<CliPipelineState, CliPipelineState> MemoryLinks(string? args = null)
        => async s =>
        {
            try
            {
                var manager = await GetMemoryManagerAsync();

                if (string.IsNullOrWhiteSpace(args))
                {
                    // Show all links
                    var allLinks = manager.Admin.CollectionLinks;
                    Console.WriteLine($"[memory] All collection links ({allLinks.Count}):");
                    foreach (var link in allLinks)
                    {
                        Console.WriteLine($"         {link.SourceCollection} ─{link.RelationType}→ {link.TargetCollection}");
                    }
                }
                else
                {
                    // Show links for specific collection
                    var links = manager.GetRelatedCollections(args.Trim());
                    Console.WriteLine($"[memory] Links for '{args.Trim()}' ({links.Count}):");
                    foreach (var link in links)
                    {
                        var direction = link.SourceCollection == args.Trim() ? "→" : "←";
                        var other = link.SourceCollection == args.Trim() ? link.TargetCollection : link.SourceCollection;
                        Console.WriteLine($"         {direction} {other} ({link.RelationType})");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[memory] Failed to get links: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Get memory statistics.
    /// Usage: MemoryStats()
    /// </summary>
    [PipelineToken("MemoryStats", "MemoryStatistics")]
    public static Step<CliPipelineState, CliPipelineState> MemoryStats(string? args = null)
        => async s =>
        {
            try
            {
                var manager = await GetMemoryManagerAsync();
                var stats = await manager.Admin.GetMemoryStatisticsAsync();

                Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
                Console.WriteLine("║           OUROBOROS MEMORY STATISTICS                 ║");
                Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
                Console.WriteLine($"║  Total Collections:     {stats.TotalCollections,10}                   ║");
                Console.WriteLine($"║  Total Vectors:         {stats.TotalVectors,10}                   ║");
                Console.WriteLine($"║  Healthy Collections:   {stats.HealthyCollections,10}                   ║");
                Console.WriteLine($"║  Unhealthy Collections: {stats.UnhealthyCollections,10}                   ║");
                Console.WriteLine($"║  Collection Links:      {stats.CollectionLinks,10}                   ║");
                Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
                Console.WriteLine("║  VECTOR DIMENSIONS:                                   ║");

                foreach (var (dim, count) in stats.DimensionDistribution.OrderByDescending(d => d.Value))
                {
                    Console.WriteLine($"║    {dim,4}d: {count,3} collections                              ║");
                }

                Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
                Console.WriteLine("║  MEMORY LAYERS:                                       ║");

                foreach (var layer in Enum.GetValues<MemoryLayer>())
                {
                    var count = await manager.GetLayerVectorCountAsync(layer);
                    Console.WriteLine($"║    {layer,-20}: {count,10} vectors          ║");
                }

                Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[memory] Failed to get statistics: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Create a memory snapshot for backup/analysis.
    /// Usage: MemorySnapshot()
    /// </summary>
    [PipelineToken("MemorySnapshot", "SnapshotMemory")]
    public static Step<CliPipelineState, CliPipelineState> MemorySnapshot(string? args = null)
        => async s =>
        {
            try
            {
                var manager = await GetMemoryManagerAsync();
                var snapshot = await manager.CreateSnapshotAsync();

                Console.WriteLine($"[memory] ✓ Snapshot created at {snapshot.Timestamp:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"         Collections: {snapshot.Collections.Count}");
                Console.WriteLine($"         Links: {snapshot.Links.Count}");
                Console.WriteLine($"         Total vectors: {snapshot.Statistics.TotalVectors}");

                // Store snapshot info in context
                s.Context = $"Memory snapshot: {snapshot.Collections.Count} collections, {snapshot.Statistics.TotalVectors} vectors";
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[memory] Failed to create snapshot: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Clear a specific memory layer (dangerous!).
    /// Usage: MemoryClear('layer_name;confirm')
    /// Example: MemoryClear('procedural;confirm')
    /// </summary>
    [PipelineToken("MemoryClear", "ClearMemoryLayer")]
    public static Step<CliPipelineState, CliPipelineState> MemoryClear(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[memory] Usage: MemoryClear('layer_name;confirm')");
                Console.WriteLine("[memory] Layers: working, episodic, semantic, procedural, autobiographical");
                return s;
            }

            try
            {
                var parts = args.Split(';');
                var layerName = parts[0].Trim().ToLowerInvariant();
                var confirmed = parts.Length > 1 && parts[1].Trim().ToLowerInvariant() == "confirm";

                var layer = layerName switch
                {
                    "working" => MemoryLayer.Working,
                    "episodic" => MemoryLayer.Episodic,
                    "semantic" => MemoryLayer.Semantic,
                    "procedural" => MemoryLayer.Procedural,
                    "autobiographical" or "self" => MemoryLayer.Autobiographical,
                    _ => (MemoryLayer?)null
                };

                if (!layer.HasValue)
                {
                    Console.WriteLine($"[memory] Unknown layer: {layerName}");
                    return s;
                }

                if (!confirmed)
                {
                    Console.WriteLine($"[memory] ⚠ This will DELETE ALL DATA in the {layer.Value} memory layer!");
                    Console.WriteLine($"[memory] To confirm, use: MemoryClear('{layerName};confirm')");
                    return s;
                }

                var manager = await GetMemoryManagerAsync();
                var success = await manager.ClearMemoryLayerAsync(layer.Value, confirmed: true);

                if (success)
                {
                    Console.WriteLine($"[memory] ✓ Cleared {layer.Value} memory layer");
                }
                else
                {
                    Console.WriteLine($"[memory] Failed to clear {layer.Value} memory layer");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[memory] Failed to clear layer: {ex.Message}");
            }

            return s;
        };
}
