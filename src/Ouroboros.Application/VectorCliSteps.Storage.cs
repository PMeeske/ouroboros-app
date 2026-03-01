// <copyright file="VectorCliSteps.Storage.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Configuration;

namespace Ouroboros.Application;

/// <summary>
/// Vector store initialization and management steps.
/// </summary>
public static partial class VectorCliSteps
{
    /// <summary>
    /// Initialize vector store from configuration or explicit type.
    /// Usage: VectorInit('Qdrant;connection=http://localhost:6334;collection=my_vectors')
    /// Usage: VectorInit('InMemory')
    /// </summary>
    [PipelineToken("VectorInit", "InitVector")]
    public static Step<CliPipelineState, CliPipelineState> VectorInit(string? args = null)
        => s =>
        {
            var parsed = ParseVectorArgs(args);

            var config = new VectorStoreConfiguration
            {
                Type = parsed.Type,
                ConnectionString = parsed.ConnectionString,
                DefaultCollection = parsed.CollectionName
            };

            try
            {
                var factory = new VectorStoreFactory(config);
                s.VectorStore = factory.Create();

                if (s.Trace) Console.WriteLine($"[vector] Initialized {config.Type} store (collection: {config.DefaultCollection})");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[vector] Failed to initialize store: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Initialize Qdrant vector store specifically.
    /// Usage: UseQdrant('http://localhost:6334;collection=my_vectors')
    /// Usage: UseQdrant() - uses default localhost:6334 with pipeline_vectors collection
    /// Note: Use semicolon (;) as separator inside quotes since pipe (|) is the DSL step separator.
    /// </summary>
    [PipelineToken("UseQdrant", "QdrantInit")]
    public static Step<CliPipelineState, CliPipelineState> UseQdrant(string? args = null)
        => s =>
        {
            var parsed = ParseVectorArgs(args);

            // Default to localhost gRPC port for Qdrant if no connection string
            string connectionString = string.IsNullOrEmpty(parsed.ConnectionString)
                ? Configuration.DefaultEndpoints.QdrantGrpc
                : parsed.ConnectionString;

            try
            {
                s.VectorStore = new QdrantVectorStore(connectionString, parsed.CollectionName);

                if (s.Trace) Console.WriteLine($"[vector] Connected to Qdrant at {connectionString} (collection: {parsed.CollectionName})");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[vector] Failed to connect to Qdrant: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Use in-memory vector store (TrackedVectorStore).
    /// Usage: UseInMemory()
    /// </summary>
    [PipelineToken("UseInMemory", "MemoryVector")]
    public static Step<CliPipelineState, CliPipelineState> UseInMemory(string? args = null)
        => s =>
        {
            s.VectorStore = new TrackedVectorStore();

            if (s.Trace) Console.WriteLine("[vector] Using in-memory vector store");

            return Task.FromResult(s);
        };

    /// <summary>
    /// Clear all vectors from the store.
    /// Usage: VectorClear()
    /// </summary>
    [PipelineToken("VectorClear", "ClearVector")]
    public static Step<CliPipelineState, CliPipelineState> VectorClear(string? args = null)
        => async s =>
        {
            if (s.VectorStore == null)
            {
                Console.WriteLine("[vector] No vector store to clear");
                return s;
            }

            try
            {
                await s.VectorStore.ClearAsync();

                if (s.Trace) Console.WriteLine("[vector] Store cleared");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[vector] Failed to clear store: {ex.Message}");
            }

            return s;
        };
}
