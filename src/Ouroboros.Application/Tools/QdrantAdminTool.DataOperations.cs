// <copyright file="QdrantAdminTool.DataOperations.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Partial class containing data mutation operations: fix, compact, and low-level CRUD helpers.
/// </summary>
public sealed partial class QdrantAdminTool
{
    private async Task<Result<string, string>> FixCollectionAsync(string? collection, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(collection))
            return Result<string, string>.Failure("Specify collection name: fix <collection>");

        if (_embedFunc == null)
            return Result<string, string>.Failure("No embedding function available for migration");

        var sb = new StringBuilder();
        sb.AppendLine($"# Fixing Collection: {collection}\n");

        try
        {
            // Get current dimension
            var testEmbed = await _embedFunc("test", ct);
            int targetDim = testEmbed.Length;

            var info = await GetCollectionInfoAsync(collection, ct);
            if (info.Dimension == targetDim)
            {
                sb.AppendLine($"✓ Collection already has correct dimension ({targetDim})");
                return Result<string, string>.Success(sb.ToString());
            }

            sb.AppendLine($"Current dimension: {info.Dimension}");
            sb.AppendLine($"Target dimension: {targetDim}");
            sb.AppendLine($"Points to migrate: {info.Points}\n");

            if (info.Points == 0)
            {
                // Just recreate empty collection
                sb.AppendLine("Recreating empty collection...");
                await _httpClient.DeleteAsync($"/collections/{collection}", ct);
                await CreateCollectionAsync(collection, targetDim, ct);
                sb.AppendLine("✓ Collection recreated with correct dimension");
            }
            else
            {
                // Migrate data
                sb.AppendLine("Migrating data with new embeddings...");

                // Scroll all points
                var points = await ScrollAllPointsAsync(collection, ct);
                sb.AppendLine($"Retrieved {points.Count} points");

                // Delete and recreate
                await _httpClient.DeleteAsync($"/collections/{collection}", ct);
                await CreateCollectionAsync(collection, targetDim, ct);

                // Re-embed and insert
                int migrated = 0;
                int failed = 0;

                foreach (var (id, payload) in points)
                {
                    try
                    {
                        string text = ExtractTextFromPayload(payload);
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            failed++;
                            continue;
                        }

                        var newEmbed = await _embedFunc(text, ct);
                        await UpsertPointAsync(collection, id, newEmbed, payload, ct);
                        migrated++;
                    }
                    catch
                    {
                        failed++;
                    }
                }

                sb.AppendLine($"\n✓ Migrated {migrated} points ({failed} failed)");
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Fix failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"Fix failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> CompactAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Compacting Qdrant Storage\n");

        try
        {
            var response = await _httpClient.GetAsync("/collections", ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            int removed = 0;
            var removedNames = new List<string>();

            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString() ?? "";
                if (!name.StartsWith("ouroboros_")) continue;

                var info = await GetCollectionInfoAsync(name, ct);

                // Remove empty backup collections
                if (info.Points == 0 && name.Contains("_backup_"))
                {
                    await _httpClient.DeleteAsync($"/collections/{name}", ct);
                    removedNames.Add(name);
                    removed++;
                }
            }

            sb.AppendLine($"**Removed {removed} empty backup collections:**");
            foreach (var name in removedNames)
                sb.AppendLine($"- {name}");

            if (removed == 0)
                sb.AppendLine("No empty backup collections to remove.");

            return Result<string, string>.Success(sb.ToString());
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Compact failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"Compact failed: {ex.Message}");
        }
    }

    private async Task<(int Points, int Dimension)> GetCollectionInfoAsync(string name, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/collections/{name}", ct);
            if (!response.IsSuccessStatusCode)
                return (0, 0);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var result = json.GetProperty("result");

            int points = result.TryGetProperty("points_count", out var pc) ? pc.GetInt32() : 0;
            int dim = 0;

            if (result.TryGetProperty("config", out var cfg) &&
                cfg.TryGetProperty("params", out var prm) &&
                prm.TryGetProperty("vectors", out var vec))
            {
                if (vec.TryGetProperty("size", out var sz))
                    dim = sz.GetInt32();
            }

            return (points, dim);
        }
        catch
        {
            return (0, 0);
        }
    }

    private async Task CreateCollectionAsync(string name, int vectorSize, CancellationToken ct)
    {
        var payload = new { vectors = new { size = vectorSize, distance = "Cosine" } };
        await _httpClient.PutAsJsonAsync($"/collections/{name}", payload, ct);
    }

    private async Task<List<(string Id, Dictionary<string, object> Payload)>> ScrollAllPointsAsync(
        string collection, CancellationToken ct)
    {
        var results = new List<(string, Dictionary<string, object>)>();
        string? offset = null;

        while (true)
        {
            var request = new { limit = 100, offset = offset, with_payload = true, with_vector = false };
            var response = await _httpClient.PostAsJsonAsync($"/collections/{collection}/points/scroll", request, ct);

            if (!response.IsSuccessStatusCode) break;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var result = json.GetProperty("result");
            var points = result.GetProperty("points");

            foreach (var point in points.EnumerateArray())
            {
                var id = point.TryGetProperty("id", out var idProp) ? idProp.ToString() : Guid.NewGuid().ToString();
                var payloadDict = new Dictionary<string, object>();

                if (point.TryGetProperty("payload", out var payloadProp))
                {
                    foreach (var prop in payloadProp.EnumerateObject())
                    {
                        payloadDict[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString() ?? "",
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.ToString()
                        };
                    }
                }

                results.Add((id, payloadDict));
            }

            if (result.TryGetProperty("next_page_offset", out var nextOffset) &&
                nextOffset.ValueKind != JsonValueKind.Null)
            {
                offset = nextOffset.ToString();
            }
            else
            {
                break;
            }
        }

        return results;
    }

    private async Task UpsertPointAsync(string collection, string id, float[] vector, Dictionary<string, object> payload, CancellationToken ct)
    {
        var point = new { id = id, vector = vector, payload = payload };
        var request = new { points = new[] { point } };
        await _httpClient.PutAsJsonAsync($"/collections/{collection}/points", request, ct);
    }

    private static string ExtractTextFromPayload(Dictionary<string, object> payload)
    {
        var textParts = new List<string>();

        // Try common text fields
        string[] textFields = { "content", "text", "description", "title", "topic", "user_message", "assistant_response", "rationale" };
        foreach (var field in textFields)
        {
            if (payload.TryGetValue(field, out var value) && value is string s && !string.IsNullOrWhiteSpace(s))
            {
                textParts.Add(s);
            }
        }

        return string.Join(" ", textParts);
    }
}
