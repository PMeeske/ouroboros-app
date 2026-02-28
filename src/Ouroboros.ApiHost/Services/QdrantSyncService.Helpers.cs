// <copyright file="QdrantSyncService.Helpers.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Json;
using System.Text.Json;

namespace Ouroboros.ApiHost.Services;

public sealed partial class QdrantSyncService
{
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
