// <copyright file="PersonDetectionEngine.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;
using System.Text.Json;
using Ouroboros.Application.Extensions;
using Ouroboros.Domain;

/// <summary>
/// Handles person detection, identification, and communication style analysis.
/// Manages the known-persons registry and Qdrant-backed persistence.
/// </summary>
public sealed partial class PersonDetectionEngine
{
    private readonly ConcurrentDictionary<string, DetectedPerson> _knownPersons = new();
    private DetectedPerson? _currentPerson;

    private readonly Qdrant.Client.QdrantClient? _qdrantClient;
    private readonly IEmbeddingModel? _embeddingModel;
    private readonly string _personCollectionName;

    /// <summary>
    /// Gets the currently detected person, if any.
    /// </summary>
    public DetectedPerson? CurrentPerson => _currentPerson;

    /// <summary>
    /// Gets all known persons.
    /// </summary>
    public IReadOnlyCollection<DetectedPerson> KnownPersons => _knownPersons.Values.ToList();

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonDetectionEngine"/> class.
    /// </summary>
    public PersonDetectionEngine(
        Qdrant.Client.QdrantClient? qdrantClient,
        IEmbeddingModel? embeddingModel,
        string personCollectionName)
    {
        _qdrantClient = qdrantClient;
        _embeddingModel = embeddingModel;
        _personCollectionName = personCollectionName;
    }

    /// <summary>
    /// Detects and identifies a person from their message.
    /// </summary>
    public async Task<PersonDetectionResult> DetectPersonAsync(
        string message,
        string[]? recentMessages = null,
        (double ZeroCrossRate, double SpeakingRate, double DynamicRange)? voiceSignature = null,
        CancellationToken ct = default)
    {
        // Extract name if explicitly stated
        var (extractedName, nameConfidence) = ExtractNameFromMessage(message);

        // Analyze communication style
        var style = AnalyzeCommunicationStyle(message, recentMessages ?? Array.Empty<string>());

        // Try to match against known persons (name -> voice -> style)
        var (matchedPerson, matchScore, matchReason) = await FindMatchingPersonAsync(
            extractedName, style, voiceSignature, ct);

        if (matchedPerson != null && matchScore > 0.6)
        {
            // Update existing person
            var updated = matchedPerson with
            {
                LastSeen = DateTime.UtcNow,
                InteractionCount = matchedPerson.InteractionCount + 1,
                Style = BlendStyles(matchedPerson.Style, style, 0.1), // Slowly update style
                Confidence = Math.Min(1.0, matchedPerson.Confidence + 0.05)
            };

            // Blend voice signature into stored average (exponential moving average)
            if (voiceSignature is var (zcr, sr, dr))
            {
                const double alpha = 0.2; // blend rate
                updated = updated with
                {
                    VoiceZeroCrossRate = updated.VoiceZeroCrossRate.HasValue
                        ? updated.VoiceZeroCrossRate.Value * (1 - alpha) + zcr * alpha : zcr,
                    VoiceSpeakingRate = updated.VoiceSpeakingRate.HasValue
                        ? updated.VoiceSpeakingRate.Value * (1 - alpha) + sr * alpha : sr,
                    VoiceDynamicRange = updated.VoiceDynamicRange.HasValue
                        ? updated.VoiceDynamicRange.Value * (1 - alpha) + dr * alpha : dr,
                };
            }

            // Update name if provided with high confidence
            if (extractedName != null && nameConfidence > 0.7 && updated.Name == null)
            {
                updated = updated with { Name = extractedName };
            }
            else if (extractedName != null && nameConfidence > 0.5 && updated.Name != extractedName)
            {
                // Add as alias
                var aliases = updated.NameAliases.ToList();
                if (!aliases.Contains(extractedName, StringComparer.OrdinalIgnoreCase))
                {
                    aliases.Add(extractedName);
                    updated = updated with { NameAliases = aliases.ToArray() };
                }
            }

            _knownPersons[updated.Id] = updated;
            _currentPerson = updated;

            // Persist updated voice data
            StorePersonAsync(updated, ct).ObserveExceptions("PersonDetection.StorePerson");

            return new PersonDetectionResult(
                Person: updated,
                IsNewPerson: false,
                NameWasProvided: extractedName != null,
                MatchConfidence: matchScore,
                MatchReason: matchReason);
        }

        // Create new person -- set initial voice data from current utterance
        var newPerson = new DetectedPerson(
            Id: Guid.NewGuid().ToString(),
            Name: extractedName,
            NameAliases: Array.Empty<string>(),
            Style: style,
            TopicInterests: ExtractTopicInterests(message),
            CommonPhrases: ExtractDistinctivePhrases(message),
            VocabularyComplexity: CalculateVocabularyComplexity(message),
            Formality: CalculateFormality(message),
            InteractionCount: 1,
            FirstSeen: DateTime.UtcNow,
            LastSeen: DateTime.UtcNow,
            VoiceZeroCrossRate: voiceSignature?.ZeroCrossRate,
            VoiceSpeakingRate: voiceSignature?.SpeakingRate,
            VoiceDynamicRange: voiceSignature?.DynamicRange,
            Confidence: extractedName != null ? 0.7 : 0.3);

        _knownPersons[newPerson.Id] = newPerson;
        _currentPerson = newPerson;

        // Store in Qdrant if available
        await StorePersonAsync(newPerson, ct);

        return new PersonDetectionResult(
            Person: newPerson,
            IsNewPerson: true,
            NameWasProvided: extractedName != null,
            MatchConfidence: 0.0,
            MatchReason: null);
    }

    /// <summary>
    /// Explicitly sets the current person by name.
    /// </summary>
    public PersonDetectionResult SetCurrentPerson(string name)
    {
        // Try to find by name
        var existing = _knownPersons.Values.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) ||
            p.NameAliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)));

        if (existing != null)
        {
            _currentPerson = existing with { LastSeen = DateTime.UtcNow };
            _knownPersons[existing.Id] = _currentPerson;
            return new PersonDetectionResult(_currentPerson, false, true, 1.0, "Explicit name match");
        }

        // Create new person with this name
        var newPerson = DetectedPerson.Unknown() with { Name = name, Confidence = 0.8 };
        _knownPersons[newPerson.Id] = newPerson;
        _currentPerson = newPerson;

        return new PersonDetectionResult(newPerson, true, true, 0.0, null);
    }

    /// <summary>
    /// Gets a known person by ID.
    /// </summary>
    public DetectedPerson? GetPerson(string personId) =>
        _knownPersons.GetValueOrDefault(personId);

    /// <summary>
    /// Loads known persons from Qdrant storage.
    /// </summary>
    public async Task LoadKnownPersonsAsync(CancellationToken ct)
    {
        if (_qdrantClient == null) return;

        try
        {
            var exists = await _qdrantClient.CollectionExistsAsync(_personCollectionName, ct);
            if (!exists) return;

            var scrollResponse = await _qdrantClient.ScrollAsync(
                _personCollectionName,
                limit: 100,
                cancellationToken: ct);

            foreach (var point in scrollResponse.Result)
            {
                try
                {
                    var payload = point.Payload;
                    var styleJson = payload.TryGetValue("style_json", out var sj) ? sj.StringValue : "{}";
                    var style = JsonSerializer.Deserialize<CommunicationStyle>(styleJson) ?? CommunicationStyle.Default;

                    var person = new DetectedPerson(
                        Id: payload.TryGetValue("person_id", out var pid) ? pid.StringValue : point.Id.Uuid,
                        Name: payload.TryGetValue("name", out var n) && !string.IsNullOrEmpty(n.StringValue) ? n.StringValue : null,
                        NameAliases: payload.TryGetValue("aliases", out var a) ? a.StringValue.Split(',', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>(),
                        Style: style,
                        TopicInterests: new Dictionary<string, double>(),
                        CommonPhrases: Array.Empty<string>(),
                        VocabularyComplexity: 0.5,
                        Formality: 0.5,
                        InteractionCount: payload.TryGetValue("interaction_count", out var ic) ? (int)ic.IntegerValue : 1,
                        FirstSeen: payload.TryGetValue("first_seen", out var fs) && DateTime.TryParse(fs.StringValue, out var fsDt) ? fsDt : DateTime.UtcNow,
                        LastSeen: payload.TryGetValue("last_seen", out var ls) && DateTime.TryParse(ls.StringValue, out var lsDt) ? lsDt : DateTime.UtcNow,
                        VoiceZeroCrossRate: payload.TryGetValue("voice_zcr", out var vzcr) && vzcr.DoubleValue != 0 ? vzcr.DoubleValue : null,
                        VoiceSpeakingRate: payload.TryGetValue("voice_speaking_rate", out var vsr) && vsr.DoubleValue != 0 ? vsr.DoubleValue : null,
                        VoiceDynamicRange: payload.TryGetValue("voice_dynamic_range", out var vdr) && vdr.DoubleValue != 0 ? vdr.DoubleValue : null,
                        Confidence: payload.TryGetValue("confidence", out var c) ? c.DoubleValue : 0.5);

                    _knownPersons[person.Id] = person;
                }
                catch (System.Text.Json.JsonException) { /* Skip malformed entries */ }
            }

            if (_knownPersons.Count > 0)
            {
                Console.WriteLine($"  [OK] Loaded {_knownPersons.Count} known person(s) from memory");
            }
        }
        catch (Grpc.Core.RpcException ex)
        {
            Console.WriteLine($"  [!] Failed to load persons: {ex.Message}");
        }
    }

    /// <summary>
    /// Stores a person in Qdrant for persistence.
    /// </summary>
    internal async Task StorePersonAsync(DetectedPerson person, CancellationToken ct)
    {
        if (_qdrantClient == null || _embeddingModel == null) return;

        try
        {
            var searchText = $"Person: {person.Name ?? "Unknown"}, Style: verbosity={person.Style.Verbosity:F2}, " +
                           $"questions={person.Style.QuestionFrequency:F2}, formality={person.Formality:F2}";

            var embedding = await _embeddingModel.CreateEmbeddingsAsync(searchText, ct);

            var payload = new Dictionary<string, Qdrant.Client.Grpc.Value>
            {
                ["person_id"] = person.Id,
                ["name"] = person.Name ?? "",
                ["aliases"] = string.Join(",", person.NameAliases),
                ["interaction_count"] = person.InteractionCount,
                ["first_seen"] = person.FirstSeen.ToString("O"),
                ["last_seen"] = person.LastSeen.ToString("O"),
                ["style_json"] = JsonSerializer.Serialize(person.Style),
                ["voice_zcr"] = person.VoiceZeroCrossRate ?? 0.0,
                ["voice_speaking_rate"] = person.VoiceSpeakingRate ?? 0.0,
                ["voice_dynamic_range"] = person.VoiceDynamicRange ?? 0.0,
                ["confidence"] = person.Confidence
            };

            var point = new Qdrant.Client.Grpc.PointStruct
            {
                Id = new Qdrant.Client.Grpc.PointId { Uuid = person.Id },
                Vectors = embedding,
                Payload = { payload }
            };

            await _qdrantClient.UpsertAsync(_personCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Grpc.Core.RpcException ex)
        {
            Console.WriteLine($"  [!] Failed to store person: {ex.Message}");
        }
    }
}
