// <copyright file="QdrantSyncService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Json;
using System.Text.Json;
using Ouroboros.Core.Configuration;
using Ouroboros.Domain.Vectors;

namespace Ouroboros.ApiHost.Services;

/// <summary>
/// Manages Qdrant Cloud sync operations: status, diff, sync with per-index
/// EC encryption, integrity verification, and collections listing.
/// </summary>
public sealed class QdrantSyncService : IQdrantSyncService, IDisposable
{
    private readonly HttpClient _localClient;
    private readonly HttpClient _cloudClient;
    private readonly string _localEndpoint;
    private readonly string _cloudEndpoint;
    private readonly bool _isConfigured;
    private readonly EcVectorCrypto? _crypto;

    public QdrantSyncService(QdrantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _localEndpoint = settings.HttpEndpoint;
        _localClient = new HttpClient
        {
            BaseAddress = new Uri(_localEndpoint),
            Timeout = TimeSpan.FromSeconds(120),
        };

        if (settings.Cloud is { Enabled: true, Endpoint.Length: > 0, ApiKey.Length: > 0 })
        {
            _cloudEndpoint = settings.Cloud.Endpoint.TrimEnd('/');
            _cloudClient = new HttpClient
            {
                BaseAddress = new Uri(_cloudEndpoint),
                Timeout = TimeSpan.FromSeconds(120),
            };
            _cloudClient.DefaultRequestHeaders.Add("api-key", settings.Cloud.ApiKey);
            _isConfigured = true;

            _crypto = !string.IsNullOrWhiteSpace(settings.Cloud.EncryptionPrivateKey)
                ? new EcVectorCrypto(settings.Cloud.EncryptionPrivateKey)
                : new EcVectorCrypto();
        }
        else
        {
            _cloudEndpoint = "";
            _cloudClient = UnconfiguredCloudClient;
        }
    }

    /// <summary>Shared placeholder client for the unconfigured cloud path (never called).</summary>
    private static readonly HttpClient UnconfiguredCloudClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    });

    // ═══════════════════════════════════════════════════════════════════════
    //  IQdrantSyncService
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<SyncStatusResponse> GetStatusAsync(CancellationToken ct)
    {
        var (localOk, localCount) = await ProbeEndpointAsync(_localClient, ct);
        var (cloudOk, cloudCount) = _isConfigured
            ? await ProbeEndpointAsync(_cloudClient, ct)
            : (false, 0);

        return new SyncStatusResponse
        {
            Local = new EndpointStatus
            {
                Endpoint = _localEndpoint,
                Online = localOk,
                CollectionCount = localCount,
            },
            Cloud = new EndpointStatus
            {
                Endpoint = _cloudEndpoint,
                Online = cloudOk,
                CollectionCount = cloudCount,
            },
            EncryptionActive = _crypto != null,
            EncryptionCurve = _crypto != null ? "NIST P-256" : null,
            Ready = _isConfigured && localOk && cloudOk && _crypto != null,
        };
    }

    public async Task<SyncDiffResponse> GetDiffAsync(CancellationToken ct)
    {
        EnsureConfigured();

        var local = await GetCollectionStatsAsync(_localClient, ct)
            ?? throw new InvalidOperationException("Cannot connect to local Qdrant.");
        var cloud = await GetCollectionStatsAsync(_cloudClient, ct)
            ?? throw new InvalidOperationException("Cannot connect to Qdrant Cloud.");

        var allNames = local.Keys.Union(cloud.Keys).OrderBy(n => n).ToList();
        var diffs = new List<CollectionDiff>();
        int synced = 0, diverged = 0, localOnly = 0, cloudOnly = 0;

        foreach (var name in allNames)
        {
            var hasLocal = local.TryGetValue(name, out var li);
            var hasCloud = cloud.TryGetValue(name, out var ci);

            string status;
            if (hasLocal && !hasCloud) { status = "local_only"; localOnly++; }
            else if (!hasLocal && hasCloud) { status = "cloud_only"; cloudOnly++; }
            else if (li.points == ci.points) { status = "synced"; synced++; }
            else { status = "diverged"; diverged++; }

            diffs.Add(new CollectionDiff
            {
                Name = name,
                LocalPoints = hasLocal ? li.points : null,
                LocalDimension = hasLocal ? li.dim : null,
                CloudPoints = hasCloud ? ci.points : null,
                CloudDimension = hasCloud ? ci.dim : null,
                Status = status,
            });
        }

        return new SyncDiffResponse
        {
            Collections = diffs,
            Synced = synced,
            Diverged = diverged,
            LocalOnly = localOnly,
            CloudOnly = cloudOnly,
        };
    }

    public async Task<SyncResultResponse> SyncAsync(string? collection, CancellationToken ct)
    {
        EnsureConfigured();
        if (_crypto == null)
            throw new InvalidOperationException("EC encryption key not available.");

        var localStats = await GetCollectionStatsAsync(_localClient, ct)
            ?? throw new InvalidOperationException("Cannot connect to local Qdrant.");

        var toSync = collection != null
            ? localStats.Where(kv => kv.Key.Equals(collection, StringComparison.OrdinalIgnoreCase)).ToList()
            : localStats.Where(kv => kv.Key.StartsWith("ouroboros_") || IsKnownCollection(kv.Key)).ToList();

        if (toSync.Count == 0)
            throw new InvalidOperationException(collection != null
                ? $"Collection '{collection}' not found locally."
                : "No ouroboros collections found locally.");

        var results = new List<CollectionSyncResult>();
        int totalSynced = 0, totalFailed = 0;

        foreach (var (name, (points, dim)) in toSync)
        {
            if (points == 0)
            {
                await EnsureCloudCollectionAsync(name, dim, ct);
                results.Add(new CollectionSyncResult { Name = name, Points = 0, Dimension = dim, Synced = 0, Failed = 0 });
                continue;
            }

            try
            {
                await EnsureCloudCollectionAsync(name, dim, ct);

                int synced = 0, failed = 0;
                string? offset = null;

                while (true)
                {
                    var batch = await ScrollPointsAsync(_localClient, name, 100, offset, ct);
                    if (batch.points.Count == 0) break;

                    if (await UpsertEncryptedPointsAsync(name, batch.points, ct))
                        synced += batch.points.Count;
                    else
                        failed += batch.points.Count;

                    offset = batch.nextOffset;
                    if (offset == null) break;
                }

                totalSynced += synced;
                totalFailed += failed;
                results.Add(new CollectionSyncResult { Name = name, Points = points, Dimension = dim, Synced = synced, Failed = failed });
            }
            catch (Exception ex)
            {
                totalFailed += points;
                results.Add(new CollectionSyncResult { Name = name, Points = points, Dimension = dim, Synced = 0, Failed = points, Error = ex.Message });
            }
        }

        return new SyncResultResponse { Collections = results, TotalSynced = totalSynced, TotalFailed = totalFailed };
    }

    public async Task<SyncVerifyResponse> VerifyAsync(string? collection, CancellationToken ct)
    {
        EnsureConfigured();
        if (_crypto == null)
            throw new InvalidOperationException("EC encryption key not available.");

        var cloudStats = await GetCollectionStatsAsync(_cloudClient, ct)
            ?? throw new InvalidOperationException("Cannot connect to Qdrant Cloud.");

        var toVerify = collection != null
            ? cloudStats.Where(kv => kv.Key.Equals(collection, StringComparison.OrdinalIgnoreCase)).ToList()
            : cloudStats.Where(kv => kv.Key.StartsWith("ouroboros_") || IsKnownCollection(kv.Key)).ToList();

        if (toVerify.Count == 0)
            throw new InvalidOperationException(collection != null
                ? $"Collection '{collection}' not found on cloud."
                : "No ouroboros collections found on cloud.");

        var results = new List<CollectionVerifyResult>();
        int totalIntact = 0, totalCorrupted = 0, totalMissing = 0;

        foreach (var (name, (points, _)) in toVerify)
        {
            if (points == 0)
            {
                results.Add(new CollectionVerifyResult { Name = name, Points = 0, Intact = 0, Corrupted = 0, MissingHmac = 0 });
                continue;
            }

            try
            {
                int intact = 0, corrupted = 0, missingHmac = 0;
                string? offset = null;

                while (true)
                {
                    var batch = await ScrollPointsAsync(_cloudClient, name, 100, offset, ct);
                    if (batch.points.Count == 0) break;

                    foreach (var point in batch.points)
                    {
                        if (!point.TryGetProperty("id", out var idProp) ||
                            !point.TryGetProperty("vector", out var vecProp))
                            continue;

                        var pointId = idProp.ValueKind == JsonValueKind.Number
                            ? idProp.GetInt64().ToString()
                            : idProp.GetString() ?? "";

                        string? storedHmac = null;
                        if (point.TryGetProperty("payload", out var payProp) &&
                            payProp.TryGetProperty("_vector_hmac", out var hmacProp))
                        {
                            storedHmac = hmacProp.GetString();
                        }

                        if (string.IsNullOrEmpty(storedHmac))
                        {
                            missingHmac++;
                            continue;
                        }

                        var floats = new float[vecProp.GetArrayLength()];
                        int idx = 0;
                        foreach (var v in vecProp.EnumerateArray())
                            floats[idx++] = v.GetSingle();

                        if (_crypto.VerifyVectorHmac(floats, pointId, storedHmac))
                            intact++;
                        else
                            corrupted++;
                    }

                    offset = batch.nextOffset;
                    if (offset == null) break;
                }

                totalIntact += intact;
                totalCorrupted += corrupted;
                totalMissing += missingHmac;
                results.Add(new CollectionVerifyResult { Name = name, Points = points, Intact = intact, Corrupted = corrupted, MissingHmac = missingHmac });
            }
            catch (Exception ex)
            {
                results.Add(new CollectionVerifyResult { Name = name, Points = points, Intact = 0, Corrupted = 0, MissingHmac = 0, Error = ex.Message });
            }
        }

        return new SyncVerifyResponse { Collections = results, TotalIntact = totalIntact, TotalCorrupted = totalCorrupted, TotalMissingHmac = totalMissing };
    }

    public async Task<SyncCollectionsResponse> ListCloudCollectionsAsync(CancellationToken ct)
    {
        EnsureConfigured();

        var stats = await GetCollectionStatsAsync(_cloudClient, ct)
            ?? throw new InvalidOperationException("Cannot connect to Qdrant Cloud.");

        var list = stats
            .OrderBy(kv => kv.Key)
            .Select(kv => new CloudCollectionInfo { Name = kv.Key, Points = kv.Value.points, Dimension = kv.Value.dim })
            .ToList();

        return new SyncCollectionsResponse
        {
            Collections = list,
            TotalCollections = list.Count,
            TotalPoints = list.Sum(c => (long)c.Points),
        };
    }

    public SyncKeyInfoResponse? GetKeyInfo()
    {
        if (_crypto == null) return null;

        var pubKey = _crypto.ExportPublicKeyBase64();
        return new SyncKeyInfoResponse
        {
            Curve = "NIST P-256 (secp256r1)",
            Mode = "Per-index keystream (ECDH self-agreement → HKDF-SHA256 → XOR per float)",
            PublicKeyFingerprint = $"{pubKey[..20]}...{pubKey[^8..]}",
            FullPublicKey = pubKey,
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private void EnsureConfigured()
    {
        if (!_isConfigured)
            throw new InvalidOperationException(
                "Qdrant Cloud sync not configured. Set Ouroboros:Qdrant:Cloud:Endpoint, ApiKey, and Enabled=true.");
    }

    private static async Task<(bool online, int count)> ProbeEndpointAsync(HttpClient client, CancellationToken ct)
    {
        try
        {
            var resp = await client.GetAsync("/collections", ct);
            if (!resp.IsSuccessStatusCode) return (false, 0);
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            int count = json.GetProperty("result").GetProperty("collections").GetArrayLength();
            return (true, count);
        }
        catch
        {
            return (false, 0);
        }
    }

    private static async Task<Dictionary<string, (int points, int dim)>?> GetCollectionStatsAsync(
        HttpClient client, CancellationToken ct)
    {
        try
        {
            var resp = await client.GetAsync("/collections", ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");
            var result = new Dictionary<string, (int, int)>();

            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString() ?? "";
                result[name] = await GetCollectionInfoAsync(client, name, ct);
            }

            return result;
        }
        catch
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
        catch
        {
            return (0, 0);
        }
    }

    private async Task EnsureCloudCollectionAsync(string name, int dim, CancellationToken ct)
    {
        var resp = await _cloudClient.GetAsync($"/collections/{name}", ct);
        if (resp.IsSuccessStatusCode) return;

        var payload = new { vectors = new { size = dim > 0 ? dim : 768, distance = "Cosine" } };
        await _cloudClient.PutAsJsonAsync($"/collections/{name}", payload, ct);
    }

    private static async Task<(List<JsonElement> points, string? nextOffset)> ScrollPointsAsync(
        HttpClient client, string collection, int limit, string? offset, CancellationToken ct)
    {
        var request = new Dictionary<string, object?> { ["limit"] = limit, ["with_payload"] = true, ["with_vector"] = true };
        if (offset != null) request["offset"] = offset;

        var resp = await client.PostAsJsonAsync($"/collections/{collection}/points/scroll", request, ct);
        if (!resp.IsSuccessStatusCode) return ([], null);

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

    private async Task<bool> UpsertEncryptedPointsAsync(
        string collection, List<JsonElement> points, CancellationToken ct)
    {
        try
        {
            var upsertPoints = new List<Dictionary<string, object?>>();

            foreach (var point in points)
            {
                if (!point.TryGetProperty("id", out var idProp) ||
                    !point.TryGetProperty("vector", out var vecProp))
                    continue;

                var pointId = idProp.ValueKind == JsonValueKind.Number
                    ? idProp.GetInt64().ToString()
                    : idProp.GetString() ?? Guid.NewGuid().ToString();

                var floats = new float[vecProp.GetArrayLength()];
                int idx = 0;
                foreach (var v in vecProp.EnumerateArray())
                    floats[idx++] = v.GetSingle();

                var encrypted = _crypto!.EncryptPerIndex(floats, pointId);
                var hmac = _crypto.ComputeVectorHmac(floats, pointId);

                var dict = new Dictionary<string, object> { ["_vector_hmac"] = hmac };
                if (point.TryGetProperty("payload", out var payProp) &&
                    payProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in payProp.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString()!,
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.ToString(),
                        };
                    }
                }

                upsertPoints.Add(new Dictionary<string, object?>
                {
                    ["id"] = idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64() : pointId,
                    ["vector"] = encrypted,
                    ["payload"] = dict,
                });
            }

            if (upsertPoints.Count == 0) return true;

            var body = new { points = upsertPoints };
            var resp = await _cloudClient.PutAsJsonAsync($"/collections/{collection}/points", body, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsKnownCollection(string name) =>
        name is "core" or "fullcore" or "codebase" or "prefix_cache" or "tools"
            or "qdrant_documentation" or "pipeline_vectors" or "distinction_states"
            or "episodic_memory" or "network_state_snapshots" or "network_learnings";

    public void Dispose()
    {
        _localClient.Dispose();
        _cloudClient.Dispose();
        _crypto?.Dispose();
    }
}
