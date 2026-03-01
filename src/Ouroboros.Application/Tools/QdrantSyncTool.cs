// <copyright file="QdrantSyncTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ouroboros.Core.Configuration;
using Ouroboros.Domain.Vectors;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Tool for Iaret to sync her local Qdrant neuro-symbolic memory to Qdrant Cloud.
/// Vectors are encrypted with ECIES (ECDH P-256 + AES-256-GCM) before upload.
/// The encrypted vector is stored in the payload as <c>_encrypted_vector</c> (base64),
/// and a placeholder unit vector fills the Qdrant vector slot to preserve collection structure.
/// </summary>
public sealed partial class QdrantSyncTool : ITool, IDisposable
{
    private readonly HttpClient _localClient;
    private readonly HttpClient _cloudClient;
    private readonly string _localBaseUrl;
    private readonly string _cloudBaseUrl;
    private readonly bool _isConfigured;
    private readonly EcVectorCrypto? _crypto;

    /// <summary>
    /// Initializes a new instance using DI-provided settings.
    /// </summary>
    public QdrantSyncTool(QdrantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _localBaseUrl = settings.HttpEndpoint;
        _localClient = new HttpClient
        {
            BaseAddress = new Uri(_localBaseUrl),
            Timeout = TimeSpan.FromSeconds(120)
        };

        if (settings.Cloud is { Enabled: true, Endpoint.Length: > 0, ApiKey.Length: > 0 })
        {
            _cloudBaseUrl = settings.Cloud.Endpoint.TrimEnd('/');
            _cloudClient = new HttpClient
            {
                BaseAddress = new Uri(_cloudBaseUrl),
                Timeout = TimeSpan.FromSeconds(120)
            };
            _cloudClient.DefaultRequestHeaders.Add("api-key", settings.Cloud.ApiKey);
            _isConfigured = true;

            // Initialize EC encryption
            if (!string.IsNullOrWhiteSpace(settings.Cloud.EncryptionPrivateKey))
            {
                _crypto = new EcVectorCrypto(settings.Cloud.EncryptionPrivateKey);
            }
            else
            {
                // Generate a new key pair — caller should persist the private key
                _crypto = new EcVectorCrypto();
                Console.WriteLine("[qdrant-sync] Generated new EC P-256 key pair for vector encryption.");
                Console.WriteLine($"[qdrant-sync] SAVE THIS PRIVATE KEY in Ouroboros:Qdrant:Cloud:EncryptionPrivateKey:");
                Console.WriteLine($"  {_crypto.ExportPrivateKeyBase64()}");
                Console.WriteLine($"[qdrant-sync] Public key: {_crypto.ExportPublicKeyBase64()}");
            }
        }
        else
        {
            _cloudBaseUrl = "";
            _cloudClient = UnconfiguredCloudClient;
            _isConfigured = false;
        }
    }

    /// <summary>Shared placeholder client for the unconfigured cloud path (never called).</summary>
    private static readonly HttpClient UnconfiguredCloudClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    });

    /// <inheritdoc/>
    public string Name => "qdrant_sync";

    /// <inheritdoc/>
    public string Description => @"Sync my local Qdrant neuro-symbolic memory to Qdrant Cloud with per-index EC encryption. Commands:
- status: Show local and cloud connection health and encryption status
- diff: Compare local vs cloud collections (names and point counts)
- sync: Push all ouroboros_* collections from local to cloud (each vector dimension encrypted via ECDH P-256 keystream)
- sync <collection>: Push a specific collection to cloud
- verify: Verify integrity of all cloud vectors via HMAC-SHA256
- verify <collection>: Verify integrity of a specific cloud collection
- collections: List cloud collections with point counts
- keyinfo: Show the current EC public key fingerprint";

    /// <inheritdoc/>
    public string? JsonSchema => """
{
  "type": "object",
  "properties": {
    "command": {
      "type": "string",
      "enum": ["status", "diff", "sync", "verify", "collections", "keyinfo"],
      "description": "The sync command to execute"
    },
    "collection": {
      "type": "string",
      "description": "Optional collection name for sync command"
    }
  },
  "required": ["command"]
}
""";

    /// <inheritdoc/>
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (!_isConfigured)
        {
            return Result<string, string>.Failure(
                "Qdrant Cloud sync not configured. Set Ouroboros:Qdrant:Cloud:Endpoint, " +
                "Ouroboros:Qdrant:Cloud:ApiKey, and Ouroboros:Qdrant:Cloud:Enabled=true in appsettings.json.");
        }

        try
        {
            string command = input.Trim().ToLowerInvariant();
            string? collection = null;

            try
            {
                using var doc = JsonDocument.Parse(input);
                if (doc.RootElement.TryGetProperty("command", out var cmdEl))
                    command = cmdEl.GetString()?.ToLowerInvariant() ?? command;
                if (doc.RootElement.TryGetProperty("collection", out var colEl))
                    collection = colEl.GetString();
            }
            catch (JsonException) { /* Use raw input */ }

            return command switch
            {
                "status" => await GetSyncStatusAsync(ct),
                "diff" => await DiffAsync(ct),
                "sync" => await SyncAsync(collection, ct),
                "verify" => await VerifyAsync(collection, ct),
                "collections" => await ListCloudCollectionsAsync(ct),
                "keyinfo" => GetKeyInfo(),
                _ => Result<string, string>.Failure($"Unknown command: {command}")
            };
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Qdrant sync error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"Qdrant sync error: {ex.Message}");
        }
    }

    private Result<string, string> GetKeyInfo()
    {
        if (_crypto == null)
            return Result<string, string>.Failure("No encryption key loaded.");

        var pubKey = _crypto.ExportPublicKeyBase64();
        var sb = new StringBuilder();
        sb.AppendLine("# EC Vector Encryption Key Info\n");
        sb.AppendLine("**Curve:** NIST P-256 (secp256r1)");
        sb.AppendLine("**Mode:** Per-index keystream (ECDH self-agreement → HKDF-SHA256 → XOR per float)");
        sb.AppendLine("**Per-point salt:** Qdrant point ID mixed into HKDF info");
        sb.AppendLine($"**Public key:** `{pubKey[..20]}...{pubKey[^8..]}`");
        sb.AppendLine($"**Full public key:** {pubKey}");
        sb.AppendLine("\nEach vector dimension is individually encrypted. Output is a float[] of the same shape, stored as a real Qdrant vector.");

        return Result<string, string>.Success(sb.ToString());
    }

    // ============ Helpers ============

    private async Task<Dictionary<string, (int points, int dim)>?> GetCollectionStatsAsync(
        HttpClient client, CancellationToken ct)
    {
        try
        {
            var resp = await client.GetAsync("/collections", ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            var result = new Dictionary<string, (int points, int dim)>();

            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString() ?? "";
                var info = await GetCollectionInfoAsync(client, name, ct);
                result[name] = info;
            }

            return result;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static async Task<(int points, int dim)> GetCollectionInfoAsync(
        HttpClient client, string name, CancellationToken ct)
    {
        try
        {
            var resp = await client.GetAsync($"/collections/{name}", ct);
            if (!resp.IsSuccessStatusCode) return (0, 0);

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            var result = json.GetProperty("result");

            int points = result.TryGetProperty("points_count", out var pc) ? pc.GetInt32() : 0;
            int dim = 0;

            if (result.TryGetProperty("config", out var cfg) &&
                cfg.TryGetProperty("params", out var prm) &&
                prm.TryGetProperty("vectors", out var vec) &&
                vec.TryGetProperty("size", out var sz))
            {
                dim = sz.GetInt32();
            }

            return (points, dim);
        }
        catch (HttpRequestException)
        {
            return (0, 0);
        }
    }

    private async Task EnsureCloudCollectionAsync(string name, int dim, CancellationToken ct)
    {
        var resp = await _cloudClient.GetAsync($"/collections/{name}", ct);
        if (resp.IsSuccessStatusCode)
            return;

        var payload = new { vectors = new { size = dim > 0 ? dim : 768, distance = "Cosine" } };
        await _cloudClient.PutAsJsonAsync($"/collections/{name}", payload, ct);
    }

    private static async Task<(List<JsonElement> points, string? nextOffset)> ScrollPointsAsync(
        HttpClient client, string collection, int limit, string? offset, CancellationToken ct)
    {
        var request = new Dictionary<string, object?> { ["limit"] = limit, ["with_payload"] = true, ["with_vector"] = true };
        if (offset != null)
            request["offset"] = offset;

        var resp = await client.PostAsJsonAsync($"/collections/{collection}/points/scroll", request, ct);
        if (!resp.IsSuccessStatusCode)
            return ([], null);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        var result = json.GetProperty("result");
        var points = result.GetProperty("points");

        var pointList = new List<JsonElement>();
        foreach (var p in points.EnumerateArray())
            pointList.Add(p);

        string? nextOffset = null;
        if (result.TryGetProperty("next_page_offset", out var no) && no.ValueKind != JsonValueKind.Null)
            nextOffset = no.ToString();

        return (pointList, nextOffset);
    }

    private static bool IsKnownCollection(string name) =>
        name is "core" or "fullcore" or "codebase" or "prefix_cache" or "tools"
            or "qdrant_documentation" or "pipeline_vectors" or "distinction_states"
            or "episodic_memory" or "network_state_snapshots" or "network_learnings";

    /// <inheritdoc/>
    public void Dispose()
    {
        _localClient.Dispose();
        _cloudClient.Dispose();
        _crypto?.Dispose();
    }
}
