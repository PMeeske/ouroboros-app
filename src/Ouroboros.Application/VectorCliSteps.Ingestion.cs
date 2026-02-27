// <copyright file="VectorCliSteps.Ingestion.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using LangChain.Databases;

namespace Ouroboros.Application;

/// <summary>
/// Vector ingestion steps for adding, ingesting files, and ingesting directories.
/// </summary>
public static partial class VectorCliSteps
{
    /// <summary>
    /// Embed and store text in the vector store.
    /// Usage: VectorAdd('text to embed and store')
    /// Usage: VectorAdd() - uses current Context
    /// </summary>
    [PipelineToken("VectorAdd", "AddVector", "Vectorize")]
    public static Step<CliPipelineState, CliPipelineState> VectorAdd(string? args = null)
        => async s =>
        {
            if (s.VectorStore == null)
            {
                // Default to in-memory if not initialized
                s.VectorStore = new TrackedVectorStore();
                if (s.Trace) Console.WriteLine("[vector] Auto-initialized in-memory store");
            }

            string text = ParseString(args);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = s.Context;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine("[vector] No text to embed");
                return s;
            }

            try
            {
                // Split text into chunks if it's long (simple chunking)
                var chunks = ChunkText(text, 500);

                foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
                {
                    var embedding = await s.Embed.CreateEmbeddingsAsync(chunk);

                    var vector = new Vector
                    {
                        Id = Guid.NewGuid().ToString(),
                        Text = chunk,
                        Embedding = embedding
                    };

                    await s.VectorStore.AddAsync(new[] { vector });

                    if (s.Trace) Console.WriteLine($"[vector] Added chunk {index + 1}/{chunks.Count} ({embedding.Length} dims)");
                }

                if (s.Trace) Console.WriteLine($"[vector] Stored {chunks.Count} chunks");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[vector] Failed to add vector: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Ingest a file into the vector store.
    /// Usage: VectorIngestFile('path/to/file.txt')
    /// </summary>
    [PipelineToken("VectorIngestFile", "IngestFile")]
    public static Step<CliPipelineState, CliPipelineState> VectorIngestFile(string? args = null)
        => async s =>
        {
            if (s.VectorStore == null)
            {
                s.VectorStore = new TrackedVectorStore();
                if (s.Trace) Console.WriteLine("[vector] Auto-initialized in-memory store");
            }

            string path = ParseString(args);
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine("[vector] No file path provided");
                return s;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"[vector] File not found: {fullPath}");
                    return s;
                }

                // Read with fallback encoding to handle special characters
                string content;
                try
                {
                    content = await File.ReadAllTextAsync(fullPath, System.Text.Encoding.UTF8);
                }
                catch (IOException)
                {
                    // Fallback: read as bytes and decode with replacement for invalid chars
                    var bytes = await File.ReadAllBytesAsync(fullPath);
                    content = System.Text.Encoding.UTF8.GetString(bytes).Replace("\uFFFD", "?");
                }

                string fileName = Path.GetFileName(fullPath);

                // Chunk the content
                var chunks = ChunkText(content, 500);

                foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
                {
                    try
                    {
                        // Sanitize chunk for embedding (remove problematic chars)
                        var sanitizedChunk = SanitizeForEmbedding(chunk);
                        if (string.IsNullOrWhiteSpace(sanitizedChunk)) continue;

                        var embedding = await s.Embed.CreateEmbeddingsAsync(sanitizedChunk);

                        var vector = new Vector
                        {
                            Id = Guid.NewGuid().ToString(),
                            Text = sanitizedChunk,
                            Embedding = embedding,
                            Metadata = new Dictionary<string, object>
                            {
                                ["source"] = fileName,
                                ["chunk_index"] = index
                            }
                        };

                        await s.VectorStore.AddAsync(new[] { vector });
                    }
                    catch (HttpRequestException chunkEx)
                    {
                        if (s.Trace) Console.WriteLine($"[vector] Skipped chunk {index}: {chunkEx.Message}");
                    }
                }

                if (s.Trace) Console.WriteLine($"[vector] Ingested {fileName}: {chunks.Count} chunks");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[vector] Failed to ingest file: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Ingest all files from a directory into the vector store.
    /// Usage: VectorIngestDir('path/to/dir;pattern=*.cs')
    /// </summary>
    [PipelineToken("VectorIngestDir", "IngestDir")]
    public static Step<CliPipelineState, CliPipelineState> VectorIngestDir(string? args = null)
        => async s =>
        {
            if (s.VectorStore == null)
            {
                s.VectorStore = new TrackedVectorStore();
                if (s.Trace) Console.WriteLine("[vector] Auto-initialized in-memory store");
            }

            var parsed = ParseDirArgs(args);

            try
            {
                string fullPath = Path.GetFullPath(parsed.Path);
                if (!Directory.Exists(fullPath))
                {
                    Console.WriteLine($"[vector] Directory not found: {fullPath}");
                    return s;
                }

                var files = Directory.GetFiles(fullPath, parsed.Pattern, SearchOption.AllDirectories);
                int totalChunks = 0;

                foreach (var file in files)
                {
                    try
                    {
                        // Read with fallback encoding
                        string content;
                        try
                        {
                            content = await File.ReadAllTextAsync(file, System.Text.Encoding.UTF8);
                        }
                        catch (IOException)
                        {
                            var bytes = await File.ReadAllBytesAsync(file);
                            content = System.Text.Encoding.UTF8.GetString(bytes).Replace("\uFFFD", "?");
                        }

                        string relativePath = Path.GetRelativePath(fullPath, file);

                        var chunks = ChunkText(content, 500);
                        int successChunks = 0;

                        foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
                        {
                            try
                            {
                                var sanitizedChunk = SanitizeForEmbedding(chunk);
                                if (string.IsNullOrWhiteSpace(sanitizedChunk)) continue;

                                var embedding = await s.Embed.CreateEmbeddingsAsync(sanitizedChunk);

                                var vector = new Vector
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Text = sanitizedChunk,
                                    Embedding = embedding,
                                    Metadata = new Dictionary<string, object>
                                    {
                                        ["source"] = relativePath,
                                        ["chunk_index"] = index
                                    }
                                };

                                await s.VectorStore.AddAsync(new[] { vector });
                                successChunks++;
                            }
                            catch (HttpRequestException)
                            {
                                // Skip problematic chunks silently
                            }
                        }

                        totalChunks += successChunks;
                        if (s.Trace) Console.WriteLine($"[vector] Ingested {relativePath}: {successChunks} chunks");
                    }
                    catch (Exception ex)
                    {
                        if (s.Trace) Console.WriteLine($"[vector] Skipped {file}: {ex.Message}");
                    }
                }

                if (s.Trace) Console.WriteLine($"[vector] Total: {files.Length} files, {totalChunks} chunks");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[vector] Failed to ingest directory: {ex.Message}");
            }

            return s;
        };
}
