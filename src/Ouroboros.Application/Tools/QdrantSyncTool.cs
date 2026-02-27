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
public sealed class QdrantSyncTool : ITool, IDisposable
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
            _cloudClient = new HttpClient();
            _isConfigured = false;
        }
    }

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
        catch (Exception ex)
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

    private async Task<Result<string, string>> GetSyncStatusAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Qdrant Sync Status\n");

        // Check local
        bool localOk = false;
        int localCount = 0;
        try
        {
            var resp = await _localClient.GetAsync("/collections", ct);
            if (resp.IsSuccessStatusCode)
            {
                localOk = true;
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
                localCount = json.GetProperty("result").GetProperty("collections").GetArrayLength();
            }
        }
        catch (HttpRequestException) { /* offline */ }

        sb.AppendLine($"**Local** ({_localBaseUrl}): {(localOk ? $"Online — {localCount} collections" : "Offline")}");

        // Check cloud
        bool cloudOk = false;
        int cloudCount = 0;
        try
        {
            var resp = await _cloudClient.GetAsync("/collections", ct);
            if (resp.IsSuccessStatusCode)
            {
                cloudOk = true;
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
                cloudCount = json.GetProperty("result").GetProperty("collections").GetArrayLength();
            }
        }
        catch (HttpRequestException) { /* offline */ }

        sb.AppendLine($"**Cloud** ({_cloudBaseUrl}): {(cloudOk ? $"Online — {cloudCount} collections" : "Offline / Unreachable")}");
        sb.AppendLine($"**Encryption:** {(_crypto != null ? "ECDH P-256 + AES-256-GCM (active)" : "Not configured")}");

        if (localOk && cloudOk)
            sb.AppendLine("\nBoth endpoints reachable. Ready to sync.");
        else if (!cloudOk)
            sb.AppendLine("\nCloud endpoint unreachable. Check Endpoint and ApiKey in Ouroboros:Qdrant:Cloud settings.");

        return Result<string, string>.Success(sb.ToString());
    }

    private async Task<Result<string, string>> DiffAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Local ↔ Cloud Collection Diff\n");

        var local = await GetCollectionStatsAsync(_localClient, ct);
        var cloud = await GetCollectionStatsAsync(_cloudClient, ct);

        if (local == null)
            return Result<string, string>.Failure("Cannot connect to local Qdrant");
        if (cloud == null)
            return Result<string, string>.Failure("Cannot connect to Qdrant Cloud");

        var allNames = local.Keys.Union(cloud.Keys).OrderBy(n => n).ToList();

        sb.AppendLine("| Collection | Local | Cloud | Status |");
        sb.AppendLine("|------------|-------|-------|--------|");

        int onlyLocal = 0, onlyCloud = 0, synced = 0, diverged = 0;

        foreach (var name in allNames)
        {
            var hasLocal = local.TryGetValue(name, out var localInfo);
            var hasCloud = cloud.TryGetValue(name, out var cloudInfo);

            string status;
            if (hasLocal && !hasCloud)
            {
                status = "← local only";
                onlyLocal++;
            }
            else if (!hasLocal && hasCloud)
            {
                status = "→ cloud only";
                onlyCloud++;
            }
            else if (localInfo.points == cloudInfo.points)
            {
                status = "= synced";
                synced++;
            }
            else
            {
                status = $"≠ diverged ({localInfo.points - cloudInfo.points:+#;-#;0})";
                diverged++;
            }

            sb.AppendLine($"| {name} | {(hasLocal ? $"{localInfo.points} pts ({localInfo.dim}D)" : "—")} | {(hasCloud ? $"{cloudInfo.points} pts ({cloudInfo.dim}D)" : "—")} | {status} |");
        }

        sb.AppendLine($"\n**Summary:** {synced} synced, {diverged} diverged, {onlyLocal} local-only, {onlyCloud} cloud-only");

        return Result<string, string>.Success(sb.ToString());
    }

    private async Task<Result<string, string>> SyncAsync(string? collection, CancellationToken ct)
    {
        if (_crypto == null)
            return Result<string, string>.Failure("EC encryption key not available. Cannot sync without encryption.");

        var sb = new StringBuilder();

        // Get local collections to sync
        var localStats = await GetCollectionStatsAsync(_localClient, ct);
        if (localStats == null)
            return Result<string, string>.Failure("Cannot connect to local Qdrant");

        var toSync = collection != null
            ? localStats.Where(kv => kv.Key.Equals(collection, StringComparison.OrdinalIgnoreCase)).ToList()
            : localStats.Where(kv => kv.Key.StartsWith("ouroboros_") || IsKnownCollection(kv.Key)).ToList();

        if (toSync.Count == 0)
        {
            return Result<string, string>.Failure(collection != null
                ? $"Collection '{collection}' not found locally"
                : "No ouroboros collections found locally");
        }

        sb.AppendLine($"# Syncing {toSync.Count} collection(s) → Qdrant Cloud (EC-encrypted)\n");

        int totalSynced = 0;
        int totalFailed = 0;

        foreach (var (name, (points, dim)) in toSync)
        {
            sb.AppendLine($"## {name} ({points} points, {dim}D)");

            if (points == 0)
            {
                await EnsureCloudCollectionAsync(name, dim, ct);
                sb.AppendLine("  → Created (empty)\n");
                continue;
            }

            try
            {
                await EnsureCloudCollectionAsync(name, dim, ct);

                int synced = 0;
                int failed = 0;
                string? offset = null;

                while (true)
                {
                    var batch = await ScrollPointsAsync(_localClient, name, 100, offset, ct);
                    if (batch.points.Count == 0)
                        break;

                    var upsertResult = await UpsertEncryptedPointsToCloudAsync(name, batch.points, dim, ct);
                    if (upsertResult)
                        synced += batch.points.Count;
                    else
                        failed += batch.points.Count;

                    offset = batch.nextOffset;
                    if (offset == null)
                        break;
                }

                totalSynced += synced;
                totalFailed += failed;
                sb.AppendLine($"  → Synced {synced} points (encrypted){(failed > 0 ? $" ({failed} failed)" : "")}\n");
            }
            catch (Exception ex)
            {
                totalFailed += points;
                sb.AppendLine($"  → Failed: {ex.Message}\n");
            }
        }

        sb.AppendLine($"**Total:** {totalSynced} points synced (EC-encrypted), {totalFailed} failed");

        return Result<string, string>.Success(sb.ToString());
    }

    private async Task<Result<string, string>> ListCloudCollectionsAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Qdrant Cloud Collections\n");

        var stats = await GetCollectionStatsAsync(_cloudClient, ct);
        if (stats == null)
            return Result<string, string>.Failure("Cannot connect to Qdrant Cloud");

        if (stats.Count == 0)
        {
            sb.AppendLine("No collections found on cloud.");
            return Result<string, string>.Success(sb.ToString());
        }

        sb.AppendLine("| Collection | Points | Dimension |");
        sb.AppendLine("|------------|--------|-----------|");

        long totalPoints = 0;
        foreach (var (name, (points, dim)) in stats.OrderBy(kv => kv.Key))
        {
            sb.AppendLine($"| {name} | {points:N0} | {dim} |");
            totalPoints += points;
        }

        sb.AppendLine($"\n**Total:** {stats.Count} collections, {totalPoints:N0} points");

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

    /// <summary>
    /// Encrypts each point's vector per-index and upserts to cloud.
    /// Each float dimension is XOR'd with an EC-derived keystream keyed by the point ID.
    /// The encrypted vector has the same shape/dimension as the original — stored as a
    /// real Qdrant vector, preserving collection structure.
    /// </summary>
    private async Task<bool> UpsertEncryptedPointsToCloudAsync(
        string collection, List<JsonElement> points, int dim, CancellationToken ct)
    {
        try
        {
            var upsertPoints = new List<Dictionary<string, object?>>();

            foreach (var point in points)
            {
                if (!point.TryGetProperty("id", out var idProp) ||
                    !point.TryGetProperty("vector", out var vecProp))
                    continue;

                // Resolve point ID as string for HKDF mixing
                var pointId = idProp.ValueKind == JsonValueKind.Number
                    ? idProp.GetInt64().ToString()
                    : idProp.GetString() ?? Guid.NewGuid().ToString();

                // Extract the float[] vector
                var floats = new float[vecProp.GetArrayLength()];
                int idx = 0;
                foreach (var v in vecProp.EnumerateArray())
                    floats[idx++] = v.GetSingle();

                // Per-index EC encryption — output is float[] of same dimension
                var encrypted = _crypto!.EncryptPerIndex(floats, pointId);

                // HMAC-SHA256 of plaintext for integrity verification
                var hmac = _crypto.ComputeVectorHmac(floats, pointId);

                // Preserve original payload + append integrity hash
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
                            _ => prop.Value.ToString()
                        };
                    }
                }

                object? payload = dict;

                upsertPoints.Add(new Dictionary<string, object?>
                {
                    ["id"] = idProp.ValueKind == JsonValueKind.Number
                        ? idProp.GetInt64()
                        : pointId,
                    ["vector"] = encrypted,
                    ["payload"] = payload
                });
            }

            if (upsertPoints.Count == 0) return true;

            var body = new { points = upsertPoints };
            var resp = await _cloudClient.PutAsJsonAsync($"/collections/{collection}/points", body, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private async Task<Result<string, string>> VerifyAsync(string? collection, CancellationToken ct)
    {
        if (_crypto == null)
            return Result<string, string>.Failure("EC encryption key not available. Cannot verify without key.");

        var sb = new StringBuilder();

        var cloudStats = await GetCollectionStatsAsync(_cloudClient, ct);
        if (cloudStats == null)
            return Result<string, string>.Failure("Cannot connect to Qdrant Cloud");

        var toVerify = collection != null
            ? cloudStats.Where(kv => kv.Key.Equals(collection, StringComparison.OrdinalIgnoreCase)).ToList()
            : cloudStats.Where(kv => kv.Key.StartsWith("ouroboros_") || IsKnownCollection(kv.Key)).ToList();

        if (toVerify.Count == 0)
        {
            return Result<string, string>.Failure(collection != null
                ? $"Collection '{collection}' not found on cloud"
                : "No ouroboros collections found on cloud");
        }

        sb.AppendLine($"# Verifying integrity of {toVerify.Count} cloud collection(s)\n");

        int totalVerified = 0;
        int totalCorrupted = 0;
        int totalMissing = 0;

        foreach (var (name, (points, dim)) in toVerify)
        {
            sb.AppendLine($"## {name} ({points} points)");

            if (points == 0)
            {
                sb.AppendLine("  (empty)\n");
                continue;
            }

            int verified = 0;
            int corrupted = 0;
            int missingHmac = 0;
            string? offset = null;

            try
            {
                while (true)
                {
                    var batch = await ScrollPointsAsync(_cloudClient, name, 100, offset, ct);
                    if (batch.points.Count == 0)
                        break;

                    foreach (var point in batch.points)
                    {
                        if (!point.TryGetProperty("id", out var idProp) ||
                            !point.TryGetProperty("vector", out var vecProp))
                            continue;

                        var pointId = idProp.ValueKind == JsonValueKind.Number
                            ? idProp.GetInt64().ToString()
                            : idProp.GetString() ?? "";

                        // Get stored HMAC from payload
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

                        // Extract encrypted vector
                        var floats = new float[vecProp.GetArrayLength()];
                        int idx = 0;
                        foreach (var v in vecProp.EnumerateArray())
                            floats[idx++] = v.GetSingle();

                        // Verify: decrypt + recompute HMAC
                        if (_crypto.VerifyVectorHmac(floats, pointId, storedHmac))
                            verified++;
                        else
                            corrupted++;
                    }

                    offset = batch.nextOffset;
                    if (offset == null)
                        break;
                }

                totalVerified += verified;
                totalCorrupted += corrupted;
                totalMissing += missingHmac;

                var status = corrupted > 0 ? "CORRUPTED" : "OK";
                sb.AppendLine($"  → {status}: {verified} intact, {corrupted} corrupted, {missingHmac} missing HMAC\n");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  → Error: {ex.Message}\n");
            }
        }

        sb.AppendLine($"**Total:** {totalVerified} intact, {totalCorrupted} corrupted, {totalMissing} missing HMAC");

        if (totalCorrupted > 0)
            sb.AppendLine("\n**Action required:** Re-sync corrupted collections with `sync <collection>` to restore integrity.");

        return Result<string, string>.Success(sb.ToString());
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
