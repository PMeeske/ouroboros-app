// <copyright file="SelfPersistence.Internal.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ouroboros.Application.Json;

namespace Ouroboros.Application.Services;

/// <summary>
/// Private helpers and nested Qdrant response types for self-persistence.
/// </summary>
public partial class SelfPersistence
{
    private async Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        if (_embeddingFunc == null) return null;

        try
        {
            return await _embeddingFunc(text);
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> PersistToFileAsync(MindStateSnapshot snapshot, CancellationToken ct)
    {
        try
        {
            var filename = $"mind_state_{snapshot.PersonaName}_{snapshot.Timestamp:yyyyMMdd_HHmmss}.json";
            var filepath = Path.Combine(_persistenceDir, filename);
            var json = JsonSerializer.Serialize(snapshot, JsonDefaults.IndentedExact);
            await File.WriteAllTextAsync(filepath, json, ct);

            // Also save as "latest"
            var latestPath = Path.Combine(_persistenceDir, $"mind_state_{snapshot.PersonaName}_latest.json");
            await File.WriteAllTextAsync(latestPath, json, ct);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<MindStateSnapshot?> RestoreFromFileAsync(string personaName, CancellationToken ct)
    {
        try
        {
            var latestPath = Path.Combine(_persistenceDir, $"mind_state_{personaName}_latest.json");
            if (!File.Exists(latestPath)) return null;

            var json = await File.ReadAllTextAsync(latestPath, ct);
            return JsonSerializer.Deserialize<MindStateSnapshot>(json);
        }
        catch
        {
            return null;
        }
    }

    private static MindStateSnapshot? ParseMindStateFromPayload(Dictionary<string, object>? payload)
    {
        if (payload == null) return null;

        try
        {
            var snapshot = new MindStateSnapshot
            {
                Timestamp = DateTime.Parse(payload.GetValueOrDefault("timestamp")?.ToString() ?? DateTime.UtcNow.ToString("O")),
                ThoughtCount = int.Parse(payload.GetValueOrDefault("thought_count")?.ToString() ?? "0"),
                PersonaName = payload.GetValueOrDefault("persona_name")?.ToString() ?? "Ouroboros",
                CurrentEmotion = new EmotionalState
                {
                    DominantEmotion = payload.GetValueOrDefault("dominant_emotion")?.ToString() ?? "Curious",
                    Valence = double.Parse(payload.GetValueOrDefault("valence")?.ToString() ?? "0"),
                    Arousal = double.Parse(payload.GetValueOrDefault("arousal")?.ToString() ?? "0.5")
                }
            };

            // Parse JSON arrays
            var factsJson = payload.GetValueOrDefault("facts_json")?.ToString();
            if (!string.IsNullOrEmpty(factsJson))
            {
                snapshot.LearnedFacts = JsonSerializer.Deserialize<List<string>>(factsJson) ?? [];
            }

            var interestsJson = payload.GetValueOrDefault("interests_json")?.ToString();
            if (!string.IsNullOrEmpty(interestsJson))
            {
                snapshot.Interests = JsonSerializer.Deserialize<List<string>>(interestsJson) ?? [];
            }

            return snapshot;
        }
        catch
        {
            return null;
        }
    }

    private static ulong GeneratePointId(DateTime timestamp)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp:O}_{Guid.NewGuid()}"));
        return BitConverter.ToUInt64(hash, 0);
    }

    // Qdrant response types
    private class QdrantScrollResponse
    {
        [JsonPropertyName("result")]
        public QdrantScrollResult? Result { get; set; }
    }

    private class QdrantScrollResult
    {
        [JsonPropertyName("points")]
        public QdrantPoint[]? Points { get; set; }
    }

    private class QdrantPoint
    {
        [JsonPropertyName("id")]
        public object? Id { get; set; }

        [JsonPropertyName("payload")]
        public Dictionary<string, object>? Payload { get; set; }
    }

    private class QdrantSearchResponse
    {
        [JsonPropertyName("result")]
        public QdrantSearchHit[]? Result { get; set; }
    }

    private class QdrantSearchHit
    {
        [JsonPropertyName("id")]
        public object? Id { get; set; }

        [JsonPropertyName("score")]
        public float Score { get; set; }

        [JsonPropertyName("payload")]
        public Dictionary<string, object>? Payload { get; set; }
    }
}
