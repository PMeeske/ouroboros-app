// <copyright file="QdrantCliSteps.Operations.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application;

/// <summary>
/// Additional CLI pipeline steps for Qdrant: recommend, delete, add-with-metadata, and helper methods.
/// </summary>
public static partial class QdrantCliSteps
{
    /// <summary>
    /// Recommends similar vectors based on positive/negative examples.
    /// Usage: Recommend('id1,id2|neg1,neg2') - positives|negatives (negatives optional)
    /// </summary>
    [PipelineToken("Recommend", "QdrantRecommend", "SimilarTo")]
    public static Step<CliPipelineState, CliPipelineState> Recommend(string? args = null)
        => async state =>
        {
            if (state.VectorStore is not IAdvancedVectorStore advStore)
            {
                Console.WriteLine("[qdrant] Advanced vector store required.");
                return state;
            }

            var argsStr = ParseString(args);
            if (string.IsNullOrWhiteSpace(argsStr))
            {
                Console.WriteLine("[qdrant] Provide positive IDs: 'id1,id2' or 'positives|negatives'");
                return state;
            }

            try
            {
                var parts = argsStr.Split('|');
                var positiveIds = parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                var negativeIds = parts.Length > 1
                    ? parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                    : null;

                if (positiveIds.Count == 0)
                {
                    Console.WriteLine("[qdrant] At least one positive ID required.");
                    return state;
                }

                var results = await advStore.RecommendAsync(positiveIds, negativeIds, state.RetrievalK);

                state.Retrieved.Clear();
                foreach (var doc in results)
                {
                    state.Retrieved.Add(doc.PageContent);
                }

                state.Context = string.Join("\n\n---\n\n", state.Retrieved);

                if (state.Trace)
                {
                    Console.WriteLine($"[qdrant] Recommend found {results.Count} results (positive: {positiveIds.Count}, negative: {negativeIds?.Count ?? 0})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[qdrant] Recommend failed: {ex.Message}");
            }

            return state;
        };

    /// <summary>
    /// Deletes vectors by ID.
    /// Usage: DeleteById('id1,id2,id3')
    /// </summary>
    [PipelineToken("DeleteById", "QdrantDelete", "RemoveVectors")]
    public static Step<CliPipelineState, CliPipelineState> DeleteById(string? args = null)
        => async state =>
        {
            if (state.VectorStore is not IAdvancedVectorStore advStore)
            {
                Console.WriteLine("[qdrant] Advanced vector store required.");
                return state;
            }

            var argsStr = ParseString(args);
            if (string.IsNullOrWhiteSpace(argsStr))
            {
                Console.WriteLine("[qdrant] Provide IDs to delete: 'id1,id2,id3'");
                return state;
            }

            try
            {
                var ids = argsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                await advStore.DeleteByIdAsync(ids);

                state.Output = $"Deleted {ids.Length} vectors";

                if (state.Trace)
                {
                    Console.WriteLine($"[qdrant] Deleted {ids.Length} vectors by ID");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[qdrant] Delete failed: {ex.Message}");
            }

            return state;
        };

    /// <summary>
    /// Deletes vectors matching a metadata filter.
    /// Usage: DeleteByFilter('key1=value1;key2=value2')
    /// </summary>
    [PipelineToken("DeleteByFilter", "FilterDelete", "RemoveByFilter")]
    public static Step<CliPipelineState, CliPipelineState> DeleteByFilter(string? args = null)
        => async state =>
        {
            if (state.VectorStore is not IAdvancedVectorStore advStore)
            {
                Console.WriteLine("[qdrant] Advanced vector store required.");
                return state;
            }

            var filter = ParseMetadataFilter(ParseString(args));
            if (filter == null || filter.Count == 0)
            {
                Console.WriteLine("[qdrant] Provide filter: 'key1=value1;key2=value2'");
                return state;
            }

            try
            {
                await advStore.DeleteByFilterAsync(filter);

                state.Output = $"Deleted vectors matching filter: {string.Join(", ", filter.Select(kv => $"{kv.Key}={kv.Value}"))}";

                if (state.Trace)
                {
                    Console.WriteLine(state.Output);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[qdrant] Delete by filter failed: {ex.Message}");
            }

            return state;
        };

    /// <summary>
    /// Adds vector with metadata.
    /// Usage: VectorAddMeta('text to embed;key1=value1;key2=value2')
    /// </summary>
    [PipelineToken("VectorAddMeta", "AddWithMeta", "MetaAdd")]
    public static Step<CliPipelineState, CliPipelineState> VectorAddMeta(string? args = null)
        => async state =>
        {
            if (state.VectorStore == null)
            {
                state.VectorStore = new TrackedVectorStore();
                if (state.Trace) Console.WriteLine("[qdrant] Auto-initialized in-memory store");
            }

            var parts = (ParseString(args) ?? "").Split(';');
            var text = parts.Length > 0 ? parts[0].Trim() : state.Context;

            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine("[qdrant] No text to embed");
                return state;
            }

            var metadata = new Dictionary<string, object>();
            for (int i = 1; i < parts.Length; i++)
            {
                var kvp = parts[i].Split('=', 2);
                if (kvp.Length == 2)
                {
                    metadata[kvp[0].Trim()] = kvp[1].Trim();
                }
            }

            try
            {
                var embedding = await state.Embed.CreateEmbeddingsAsync(text);
                var vector = new LangChain.Databases.Vector
                {
                    Id = Guid.NewGuid().ToString(),
                    Text = text,
                    Embedding = embedding,
                    Metadata = metadata
                };

                await state.VectorStore.AddAsync(new[] { vector });

                state.Output = $"Added vector with {metadata.Count} metadata fields";

                if (state.Trace)
                {
                    Console.WriteLine($"[qdrant] Added vector ({embedding.Length}D) with metadata: {string.Join(", ", metadata.Keys)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[qdrant] Add with metadata failed: {ex.Message}");
            }

            return state;
        };

    // ============ Helper Methods ============

    private static string? ParseString(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        return input.Trim().Trim('\'', '"');
    }

    private static (string query, Dictionary<string, object>? filter, float? scoreThreshold) ParseFilterArgs(
        string? args, CliPipelineState state)
    {
        var argsStr = ParseString(args) ?? "";
        var parts = argsStr.Split(';');

        var query = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])
            ? parts[0].Trim()
            : state.Query;

        Dictionary<string, object>? filter = null;
        float? scoreThreshold = null;

        for (int i = 1; i < parts.Length; i++)
        {
            var part = parts[i].Trim();

            // Check for score threshold
            if (part.StartsWith("score>", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("threshold=", StringComparison.OrdinalIgnoreCase))
            {
                var valueStr = part.Contains('=') ? part.Split('=')[1] : part[6..];
                if (float.TryParse(valueStr, out var threshold))
                {
                    scoreThreshold = threshold;
                }
            }
            // Otherwise treat as filter
            else if (part.Contains('='))
            {
                filter ??= new Dictionary<string, object>();
                var kvp = part.Split('=', 2);
                filter[kvp[0].Trim()] = kvp[1].Trim();
            }
        }

        return (query, filter, scoreThreshold);
    }

    private static Dictionary<string, object>? ParseMetadataFilter(string? filterStr)
    {
        if (string.IsNullOrWhiteSpace(filterStr))
        {
            return null;
        }

        var filter = new Dictionary<string, object>();
        var parts = filterStr.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var kvp = part.Split('=', 2);
            if (kvp.Length == 2)
            {
                filter[kvp[0].Trim()] = kvp[1].Trim();
            }
        }

        return filter.Count > 0 ? filter : null;
    }
}
