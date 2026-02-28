// <copyright file="FileIndexTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text.Json;
using Ouroboros.Application.Services;

/// <summary>
/// Index files or directories for semantic search.
/// </summary>
internal class FileIndexTool : ITool
{
    public string Name => "index_files";
    public string Description => "Index files/directories for semantic search. Input: JSON {\"path\":\"...\", \"recursive\":true} or just a path. Returns number of chunks indexed.";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (SystemAccessTools.SharedIndexer == null)
        {
            return Result<string, string>.Failure("Self-indexer not available. Qdrant may not be connected.");
        }

        try
        {
            string path;
            bool recursive = true;

            // Try to parse as JSON first
            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(input);
                path = Environment.ExpandEnvironmentVariables(args.GetProperty("path").GetString() ?? ".");
                recursive = !args.TryGetProperty("recursive", out var recEl) || recEl.GetBoolean();
            }
            catch
            {
                // Plain text path
                path = Environment.ExpandEnvironmentVariables(input.Trim().Trim('"'));
            }

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return Result<string, string>.Failure($"Path not found: {path}");
            }

            var startTime = DateTime.UtcNow;
            int totalChunks;

            if (File.Exists(path))
            {
                // Single file
                totalChunks = await SystemAccessTools.SharedIndexer.IndexPathAsync(path, ct);
                var elapsed = DateTime.UtcNow - startTime;
                return Result<string, string>.Success(
                    $"Indexed '{Path.GetFileName(path)}' -> {totalChunks} chunks in {elapsed.TotalSeconds:F1}s");
            }
            else
            {
                // Directory
                totalChunks = await SystemAccessTools.SharedIndexer.IndexPathAsync(path, ct);
                var elapsed = DateTime.UtcNow - startTime;
                return Result<string, string>.Success(
                    $"Indexed directory '{path}' -> {totalChunks} chunks in {elapsed.TotalSeconds:F1}s");
            }
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Indexing failed: {ex.Message}");
        }
        catch (IOException ex)
        {
            return Result<string, string>.Failure($"Indexing failed: {ex.Message}");
        }
    }
}
