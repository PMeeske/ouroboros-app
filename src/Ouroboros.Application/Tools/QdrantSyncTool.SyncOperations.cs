// <copyright file="QdrantSyncTool.SyncOperations.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

public sealed partial class QdrantSyncTool
{
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
}
