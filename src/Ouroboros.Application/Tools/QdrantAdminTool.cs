// <copyright file="QdrantAdminTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ouroboros.Core.Configuration;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Tool for Ouroboros to self-administer its Qdrant vector database.
/// Enables autonomous collection management, dimension adjustment, and neuro-symbolic health checks.
/// </summary>
/// <remarks>
/// This class is split into partial files:
/// - QdrantAdminTool.cs (this file): Core ITool implementation and command dispatch
/// - QdrantAdminTool.DataOperations.cs: Data mutation operations (fix, compact, CRUD helpers)
/// - QdrantAdminTool.Analysis.cs: Query, diagnostic, and compression analysis operations
/// </remarks>
public sealed partial class QdrantAdminTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly IQdrantCollectionRegistry? _registry;
    private readonly Func<string, CancellationToken, Task<float[]>>? _embedFunc;
    private readonly Func<string, CancellationToken, Task<string>>? _llmFunc;

    /// <summary>
    /// Initializes a new instance using the DI-provided settings and collection registry.
    /// </summary>
    public QdrantAdminTool(
        QdrantSettings settings,
        IQdrantCollectionRegistry registry,
        Func<string, CancellationToken, Task<float[]>>? embedFunc = null,
        Func<string, CancellationToken, Task<string>>? llmFunc = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _baseUrl = settings.HttpEndpoint;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(60)
        };
        _embedFunc = embedFunc;
        _llmFunc = llmFunc;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantAdminTool"/> class.
    /// </summary>
    [Obsolete("Use the constructor accepting QdrantSettings + IQdrantCollectionRegistry from DI.")]
    public QdrantAdminTool(
        string qdrantEndpoint = Configuration.DefaultEndpoints.QdrantRest,
        Func<string, CancellationToken, Task<float[]>>? embedFunc = null,
        Func<string, CancellationToken, Task<string>>? llmFunc = null)
    {
        _baseUrl = qdrantEndpoint.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(60)
        };
        _embedFunc = embedFunc;
        _llmFunc = llmFunc;
    }

    /// <inheritdoc/>
    public string Name => "qdrant_admin";

    /// <inheritdoc/>
    public string Description => @"Administrate my Qdrant neuro-symbolic memory. Commands:
- status: Get all collections and their health
- diagnose: Check for dimension mismatches and issues
- fix <collection>: Auto-fix dimension mismatch by migrating data
- compact: Remove empty collections and optimize storage
- stats: Detailed statistics about my memory usage
- collections: List all collections with point counts
- compress: Analyze vector compression potential using Fourier/DCT transforms";

    /// <inheritdoc/>
    public string? JsonSchema => """
{
  "type": "object",
  "properties": {
    "command": {
      "type": "string",
      "enum": ["status", "diagnose", "fix", "compact", "stats", "collections", "compress"],
      "description": "The admin command to execute"
    },
    "collection": {
      "type": "string",
      "description": "Collection name for fix/compress commands"
    }
  },
  "required": ["command"]
}
""";

    /// <inheritdoc/>
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            string command = input.Trim().ToLowerInvariant();
            string? collection = null;

            // Try JSON parsing
            try
            {
                using var doc = JsonDocument.Parse(input);
                if (doc.RootElement.TryGetProperty("command", out var cmdEl))
                    command = cmdEl.GetString()?.ToLowerInvariant() ?? command;
                if (doc.RootElement.TryGetProperty("collection", out var colEl))
                    collection = colEl.GetString();
            }
            catch (System.Text.Json.JsonException) { /* Use raw input */ }

            return command switch
            {
                "status" => await GetStatusAsync(ct),
                "diagnose" => await DiagnoseAsync(ct),
                "fix" => await FixCollectionAsync(collection, ct),
                "compact" => await CompactAsync(ct),
                "stats" => await GetStatsAsync(ct),
                "collections" => await ListCollectionsAsync(ct),
                "compress" => await AnalyzeCompressionAsync(collection, ct),
                _ => Result<string, string>.Failure($"Unknown command: {command}")
            };
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Qdrant admin error: {ex.Message}");
        }
    }
}
