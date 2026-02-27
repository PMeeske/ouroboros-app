// <copyright file="QdrantSyncTool.CloudOperations.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

public sealed partial class QdrantSyncTool
{
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
}
