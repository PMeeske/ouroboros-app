// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Services.RoomPresence;

/// <summary>
/// Identifies and enrolls speakers by their acoustic voice signature.
///
/// Works alongside <see cref="PersonIdentifier"/> (text-style fingerprinting):
/// voice biometrics provide fast, early-stage speaker labelling; text style
/// refines it over multiple utterances.
///
/// Enrollment:
///   - The user can say "Iaret, remember my voice" or "that's me" while identified
///     by PersonIdentifier, which calls <see cref="EnrollOwner"/> to pin their
///     acoustic profile as the primary user ("User").
///   - Subsequent utterances within the same similarity threshold are labelled "User".
///
/// Matching uses cosine similarity in the (ZeroCrossRate, SpeakingRate, DynamicRange)
/// subspace — speaker-intrinsic features that don't vary with mic distance.
/// </summary>
public sealed class VoiceSignatureService
{
    private const double MatchThreshold  = 0.82;   // min similarity to call a match
    private const int    MaxSamplesKept  = 12;      // rolling average window
    private const string OwnerLabel      = "User";

    private readonly Dictionary<string, SpeakerProfile> _profiles = new();
    private string? _ownerId;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>True if an owner has been enrolled.</summary>
    public bool HasOwner => _ownerId != null;

    /// <summary>
    /// Attempts to match <paramref name="sig"/> against all known profiles.
    /// Returns (speakerId, isOwner) or null when no profile matches.
    /// </summary>
    public (string SpeakerId, bool IsOwner)? TryMatch(VoiceSignature sig)
    {
        string? bestId   = null;
        double  bestSim  = MatchThreshold - 0.001; // must beat this

        foreach (var (id, profile) in _profiles)
        {
            var sim = profile.AverageSimilarity(sig);
            if (sim > bestSim)
            {
                bestSim = sim;
                bestId  = id;
            }
        }

        if (bestId == null) return null;
        return (bestId, bestId == _ownerId);
    }

    /// <summary>
    /// Adds a sample to an existing profile or creates a new one.
    /// Returns the speaker label ("User" if this is the owner, else speakerId).
    /// </summary>
    public string AddSample(string speakerId, VoiceSignature sig)
    {
        if (!_profiles.TryGetValue(speakerId, out var profile))
        {
            profile = new SpeakerProfile(speakerId);
            _profiles[speakerId] = profile;
        }

        profile.AddSample(sig);
        return speakerId == _ownerId ? OwnerLabel : speakerId;
    }

    /// <summary>
    /// Enrolls <paramref name="speakerId"/> as the primary user ("User").
    /// Calling this again re-assigns the owner label to a different speaker.
    /// </summary>
    public void EnrollOwner(string speakerId, VoiceSignature sig)
    {
        _ownerId = speakerId;
        AddSample(speakerId, sig);
        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.WriteLine($"\n  [voice] Owner enrolled — I'll recognise your voice from now on.");
        Console.ResetColor();
    }

    /// <summary>
    /// Returns true if <paramref name="speakerId"/> is the enrolled owner.
    /// </summary>
    public bool IsOwner(string speakerId) => speakerId == _ownerId;

    /// <summary>
    /// Checks if an utterance text is an enrollment request directed at Iaret.
    /// ("Iaret, remember my voice" / "remember my voice" / "enroll my voice" / "that's me")
    /// </summary>
    public static bool IsEnrollmentRequest(string text, string personaName)
    {
        var t = text.Trim();
        return t.Contains("remember my voice",  StringComparison.OrdinalIgnoreCase) ||
               t.Contains("enroll my voice",    StringComparison.OrdinalIgnoreCase) ||
               t.Contains("that's me",          StringComparison.OrdinalIgnoreCase) ||
               t.Contains("that is me",         StringComparison.OrdinalIgnoreCase) ||
               (t.Contains(personaName, StringComparison.OrdinalIgnoreCase) &&
                t.Contains("recognise me",      StringComparison.OrdinalIgnoreCase)) ||
               (t.Contains(personaName, StringComparison.OrdinalIgnoreCase) &&
                t.Contains("recognize me",      StringComparison.OrdinalIgnoreCase));
    }

    // ── Internal speaker profile ──────────────────────────────────────────────

    private sealed class SpeakerProfile(string id)
    {
        private readonly string _id = id;
        private readonly List<VoiceSignature> _samples = [];

        public void AddSample(VoiceSignature sig)
        {
            _samples.Add(sig);
            if (_samples.Count > MaxSamplesKept)
                _samples.RemoveAt(0);
        }

        public double AverageSimilarity(VoiceSignature sig)
        {
            if (_samples.Count == 0) return 0;
            return _samples.Average(s => s.SimilarityTo(sig));
        }
    }
}
