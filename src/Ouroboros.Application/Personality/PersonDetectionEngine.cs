// <copyright file="PersonDetectionEngine.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;
using System.Text.Json;
using Ouroboros.Domain;

/// <summary>
/// Handles person detection, identification, and communication style analysis.
/// Manages the known-persons registry and Qdrant-backed persistence.
/// </summary>
public sealed class PersonDetectionEngine
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
            _ = StorePersonAsync(updated, ct);

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
                catch { /* Skip malformed entries */ }
            }

            if (_knownPersons.Count > 0)
            {
                Console.WriteLine($"  [OK] Loaded {_knownPersons.Count} known person(s) from memory");
            }
        }
        catch (Exception ex)
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
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Failed to store person: {ex.Message}");
        }
    }

    // ============================================================
    //  Internal helper methods
    // ============================================================

    /// <summary>
    /// Extracts a name from a message (multilingual).
    /// </summary>
    internal static (string? Name, double Confidence) ExtractNameFromMessage(string message)
    {
        // Multilingual name introduction patterns
        var patterns = new[]
        {
            // English
            (@"(?:my name is|i'm|i am|call me|this is)\s+([A-Z][a-z\u00e4\u00f6\u00fc\u00df\u00e1\u00e9\u00ed\u00f3\u00fa\u00e0\u00e8\u00ec\u00f2\u00f9\u00e2\u00ea\u00ee\u00f4\u00fb\u00f1\u00e7]+(?:\s+[A-Z][a-z\u00e4\u00f6\u00fc\u00df\u00e1\u00e9\u00ed\u00f3\u00fa\u00e0\u00e8\u00ec\u00f2\u00f9\u00e2\u00ea\u00ee\u00f4\u00fb\u00f1\u00e7]+)?)", 0.9),
            (@"(?:it'?s me),?\s+([A-Z][a-z\u00e4\u00f6\u00fc\u00df\u00e1\u00e9\u00ed\u00f3\u00fa\u00e0\u00e8\u00ec\u00f2\u00f9\u00e2\u00ea\u00ee\u00f4\u00fb\u00f1\u00e7]+)", 0.9),
            (@"^([A-Z][a-z\u00e4\u00f6\u00fc\u00df]+)\s+here\.?$", 0.8),
            (@"(?:^|\.\s+)([A-Z][a-z\u00e4\u00f6\u00fc\u00df]+)\s+speaking\.?", 0.85),
            (@"(?:hey|hi|hello),?\s+(?:it's|its|this is)\s+([A-Z][a-z\u00e4\u00f6\u00fc\u00df]+)", 0.85),
            // German
            (@"(?:ich bin|mein name ist|ich hei\u00dfe|ich heisse|nennen sie mich|nenn mich)\s+([A-Z\u00c4\u00d6\u00dc][a-z\u00e4\u00f6\u00fc\u00df]+)", 0.9),
            (@"(?:hier ist|hier spricht)\s+([A-Z\u00c4\u00d6\u00dc][a-z\u00e4\u00f6\u00fc\u00df]+)", 0.85),
            // French
            (@"(?:je m'appelle|je suis|mon nom est|appelez-moi)\s+([A-Z\u00c0\u00c2\u00c7\u00c9\u00c8\u00ca\u00cb\u00cf\u00ce\u00d4\u00d9\u00db\u00dc][a-z\u00e0\u00e2\u00e7\u00e9\u00e8\u00ea\u00eb\u00ef\u00ee\u00f4\u00f9\u00fb\u00fc]+)", 0.9),
            (@"(?:c'est|ici)\s+([A-Z\u00c0\u00c2\u00c7\u00c9\u00c8\u00ca\u00cb\u00cf\u00ce\u00d4\u00d9\u00db\u00dc][a-z\u00e0\u00e2\u00e7\u00e9\u00e8\u00ea\u00eb\u00ef\u00ee\u00f4\u00f9\u00fb\u00fc]+)", 0.8),
            // Spanish
            (@"(?:me llamo|soy|mi nombre es|ll\u00e1mame)\s+([A-Z\u00c1\u00c9\u00cd\u00d3\u00da\u00d1\u00dc][a-z\u00e1\u00e9\u00ed\u00f3\u00fa\u00f1\u00fc]+)", 0.9),
            // Italian
            (@"(?:mi chiamo|sono|il mio nome \u00e8|chiamami)\s+([A-Z\u00c0\u00c8\u00c9\u00cc\u00cd\u00ce\u00d2\u00d3\u00d9\u00da][a-z\u00e0\u00e8\u00e9\u00ec\u00ed\u00ee\u00f2\u00f3\u00f9\u00fa]+)", 0.9),
            // Dutch
            (@"(?:ik ben|mijn naam is|ik heet|noem me)\s+([A-Z][a-z]+)", 0.9),
            // Portuguese
            (@"(?:eu sou|meu nome \u00e9|me chamo|chama-me)\s+([A-Z\u00c1\u00c0\u00c2\u00c3\u00c9\u00ca\u00cd\u00d3\u00d4\u00d5\u00da][a-z\u00e1\u00e0\u00e2\u00e3\u00e9\u00ea\u00ed\u00f3\u00f4\u00f5\u00fa]+)", 0.9),
        };

        foreach (var (pattern, confidence) in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(message, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var name = match.Groups[1].Value.Trim();
                // Validate it's not a common word (multilingual)
                var commonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // English
                    "The", "This", "That", "What", "When", "Where", "Why", "How", "Can", "Could", "Would", "Should", "Will", "Just", "Please",
                    // German
                    "Das", "Der", "Die", "Was", "Wann", "Wo", "Warum", "Wie", "Kann", "Bitte", "Hier", "Jetzt",
                    // French
                    "Le", "La", "Les", "Que", "Quoi", "Quand", "Pourquoi", "Comment", "Peut", "Veuillez",
                    // Spanish
                    "El", "La", "Los", "Las", "Que", "Cuando", "Donde", "Por", "Como", "Puede",
                    // Italian
                    "Il", "Lo", "La", "Che", "Cosa", "Quando", "Dove", "Perch\u00e9", "Come", "Pu\u00f2"
                };
                if (!commonWords.Contains(name) && name.Length >= 2)
                {
                    return (name, confidence);
                }
            }
        }

        return (null, 0.0);
    }

    /// <summary>
    /// Analyzes communication style from messages.
    /// </summary>
    internal static CommunicationStyle AnalyzeCommunicationStyle(string message, string[] recentMessages)
    {
        var allMessages = recentMessages.Append(message).ToArray();
        var allText = string.Join(" ", allMessages);

        // Verbosity: words per message
        double avgLength = allMessages.Average(m => m.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        double verbosity = Math.Min(1.0, avgLength / 50.0);

        // Question frequency
        int questionCount = allMessages.Count(m => m.Contains('?'));
        double questionFreq = (double)questionCount / allMessages.Length;

        // Emoticon usage
        var emoticonPatterns = new[] { ":)", ":(", ":D", ";)", ":P", "\ud83d\ude00", "\ud83d\ude0a", "\ud83d\udc4d", "\u2764", "\ud83d\ude42", "\ud83d\ude02", "\ud83e\udd14" };
        int emoticonCount = emoticonPatterns.Sum(e => allMessages.Count(m => m.Contains(e)));
        double emoticonUsage = Math.Min(1.0, emoticonCount / (double)allMessages.Length);

        // Punctuation style
        int exclamationCount = allText.Count(c => c == '!');
        int multiPunctCount = System.Text.RegularExpressions.Regex.Matches(allText, @"[!?]{2,}").Count;
        double punctStyle = Math.Min(1.0, (exclamationCount + multiPunctCount * 2) / (double)allMessages.Length / 3.0);

        // Greetings and closings
        var greetings = ExtractGreetings(allMessages);
        var closings = ExtractClosings(allMessages);

        return new CommunicationStyle(
            Verbosity: verbosity,
            QuestionFrequency: questionFreq,
            EmoticonUsage: emoticonUsage,
            PunctuationStyle: punctStyle,
            AverageMessageLength: avgLength * 5, // Approximate characters
            PreferredGreetings: greetings,
            PreferredClosings: closings);
    }

    private static string[] ExtractGreetings(string[] messages)
    {
        var greetingPatterns = new[] { "hi", "hello", "hey", "greetings", "good morning", "good afternoon", "good evening", "howdy", "yo" };
        return messages
            .Select(m => m.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(w => w != null && greetingPatterns.Any(g => w.StartsWith(g)))
            .Distinct()
            .Take(3)
            .ToArray()!;
    }

    private static string[] ExtractClosings(string[] messages)
    {
        var closingPatterns = new[] { "thanks", "thank you", "cheers", "bye", "goodbye", "later", "take care", "best", "regards" };
        return messages
            .SelectMany(m => closingPatterns.Where(c => m.ToLowerInvariant().Contains(c)))
            .Distinct()
            .Take(3)
            .ToArray();
    }

    private static CommunicationStyle BlendStyles(CommunicationStyle existing, CommunicationStyle newStyle, double weight)
    {
        return new CommunicationStyle(
            Verbosity: existing.Verbosity * (1 - weight) + newStyle.Verbosity * weight,
            QuestionFrequency: existing.QuestionFrequency * (1 - weight) + newStyle.QuestionFrequency * weight,
            EmoticonUsage: existing.EmoticonUsage * (1 - weight) + newStyle.EmoticonUsage * weight,
            PunctuationStyle: existing.PunctuationStyle * (1 - weight) + newStyle.PunctuationStyle * weight,
            AverageMessageLength: existing.AverageMessageLength * (1 - weight) + newStyle.AverageMessageLength * weight,
            PreferredGreetings: existing.PreferredGreetings.Union(newStyle.PreferredGreetings).Distinct().Take(5).ToArray(),
            PreferredClosings: existing.PreferredClosings.Union(newStyle.PreferredClosings).Distinct().Take(5).ToArray());
    }

    private async Task<(DetectedPerson? Person, double Score, string? Reason)> FindMatchingPersonAsync(
        string? name,
        CommunicationStyle style,
        (double ZeroCrossRate, double SpeakingRate, double DynamicRange)? voiceSignature,
        CancellationToken ct)
    {
        // Strategy 1: Exact name match (highest confidence)
        if (name != null)
        {
            var nameMatch = _knownPersons.Values.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) ||
                p.NameAliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)));

            if (nameMatch != null)
                return (nameMatch, 0.95, $"Name match: {name}");
        }

        // Strategy 2: Voice signature match (biometric -- high confidence)
        if (voiceSignature is var (zcr, sr, dr))
        {
            var voiceMatch = _knownPersons.Values
                .Where(p => p.VoiceZeroCrossRate.HasValue)
                .Select(p => (Person: p, Score: p.VoiceSimilarityTo(zcr, sr, dr)))
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (voiceMatch.Score >= 0.78)
                return (voiceMatch.Person, 0.85, $"Voice match: {voiceMatch.Score:P0}");
        }

        // Strategy 3: Style matching against ALL known persons
        var candidates = _knownPersons.Values.ToList();
        if (candidates.Count == 0)
            return (null, 0.0, null);

        var bestMatch = candidates
            .Select(p =>
            {
                double styleSim = p.Style.SimilarityTo(style);
                double daysSince = (DateTime.UtcNow - p.LastSeen).TotalDays;
                double recencyBoost = 0.10 * Math.Exp(-daysSince / 3.0);
                return (Person: p, Score: styleSim + recencyBoost);
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestMatch.Score > 0.75)
            return (bestMatch.Person, bestMatch.Score, $"Style similarity: {bestMatch.Score:P0}");

        // Strategy 4: Soft match
        if (name != null && bestMatch.Score > 0.50)
        {
            if (bestMatch.Person.Name == null)
                return (bestMatch.Person, 0.70, $"Soft match (unnamed + name provided): style {bestMatch.Score:P0}");
        }

        return (null, 0.0, null);
    }

    private static Dictionary<string, double> ExtractTopicInterests(string message)
    {
        var topics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var topicPatterns = new Dictionary<string, string[]>
        {
            ["programming"] = new[] { "code", "programming", "developer", "software", "api", "function", "class" },
            ["ai"] = new[] { "ai", "machine learning", "neural", "gpt", "llm", "model", "training" },
            ["data"] = new[] { "data", "database", "sql", "analytics", "visualization" },
            ["web"] = new[] { "website", "web", "html", "css", "javascript", "frontend", "backend" },
            ["devops"] = new[] { "docker", "kubernetes", "deploy", "ci/cd", "pipeline", "server" },
        };

        var lower = message.ToLowerInvariant();
        foreach (var (topic, keywords) in topicPatterns)
        {
            int matches = keywords.Count(k => lower.Contains(k));
            if (matches > 0)
            {
                topics[topic] = Math.Min(1.0, matches * 0.3);
            }
        }

        return topics;
    }

    private static string[] ExtractDistinctivePhrases(string message)
    {
        // Extract 2-3 word phrases that might be distinctive
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 3) return Array.Empty<string>();

        var phrases = new List<string>();
        for (int i = 0; i < words.Length - 1; i++)
        {
            phrases.Add($"{words[i]} {words[i + 1]}");
        }

        return phrases.Take(5).ToArray();
    }

    private static double CalculateVocabularyComplexity(string message)
    {
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return 0.5;

        double avgWordLength = words.Average(w => w.Length);
        // Normalize: 3-4 chars = simple (0.2), 7+ chars = complex (0.8)
        return Math.Clamp((avgWordLength - 3) / 5.0, 0.0, 1.0);
    }

    private static double CalculateFormality(string message)
    {
        var lower = message.ToLowerInvariant();

        // Informal indicators
        var informal = new[] { "gonna", "wanna", "gotta", "kinda", "sorta", "yeah", "yep", "nope", "lol", "omg", "btw" };
        int informalCount = informal.Count(i => lower.Contains(i));

        // Formal indicators
        var formal = new[] { "please", "thank you", "would you", "could you", "i would appreciate", "kindly", "regards" };
        int formalCount = formal.Count(f => lower.Contains(f));

        if (informalCount + formalCount == 0) return 0.5;
        return (double)formalCount / (informalCount + formalCount);
    }
}
