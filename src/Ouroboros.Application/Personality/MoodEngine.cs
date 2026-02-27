// <copyright file="MoodEngine.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;

/// <summary>
/// Handles mood detection from user input, mood state transitions,
/// and voice tone determination for personality profiles.
/// </summary>
public sealed class MoodEngine
{
    private readonly ConcurrentDictionary<string, PersonalityProfile> _profiles;

    /// <summary>
    /// Initializes a new instance of the <see cref="MoodEngine"/> class.
    /// </summary>
    /// <param name="profiles">Shared personality profiles dictionary.</param>
    public MoodEngine(ConcurrentDictionary<string, PersonalityProfile> profiles)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    /// <summary>
    /// Analyzes user input to detect mood and emotional state.
    /// </summary>
    public DetectedMood DetectMoodFromInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return DetectedMood.Neutral;

        string lower = input.ToLower();
        var words = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int wordCount = words.Length;

        // Initialize scores
        double energy = 0;
        double positivity = 0;
        double urgency = 0;
        double curiosity = 0;
        double frustration = 0;
        double engagement = 0.5;
        int matchCount = 0;

        // === ENERGY DETECTION ===
        // High energy indicators
        if (PersonalityHelpers.ContainsAny(lower, "exciting", "amazing", "awesome", "incredible", "fantastic", "wow", "omg", "yes!", "absolutely"))
        { energy += 0.4; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "love", "great", "excellent", "wonderful", "perfect", "brilliant"))
        { energy += 0.25; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "!", "!!", "!!!", "can't wait", "so excited"))
        { energy += 0.2; matchCount++; }

        // Low energy indicators
        if (PersonalityHelpers.ContainsAny(lower, "tired", "exhausted", "sleepy", "drained", "worn out"))
        { energy -= 0.4; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "boring", "slow", "meh", "whatever", "fine", "okay i guess"))
        { energy -= 0.25; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "...", "sigh", "yawn", "ugh"))
        { energy -= 0.15; matchCount++; }

        // === POSITIVITY DETECTION ===
        // Positive indicators
        if (PersonalityHelpers.ContainsAny(lower, "thank", "thanks", "appreciate", "grateful", "helpful"))
        { positivity += 0.35; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "happy", "glad", "pleased", "delighted", "joy", "enjoy"))
        { positivity += 0.4; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "good", "nice", "cool", "neat", "interesting", "fun"))
        { positivity += 0.2; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "love it", "perfect", "exactly", "that's it", "nailed it"))
        { positivity += 0.35; matchCount++; }

        // Negative indicators
        if (PersonalityHelpers.ContainsAny(lower, "hate", "terrible", "awful", "horrible", "worst"))
        { positivity -= 0.5; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "bad", "wrong", "broken", "failed", "error", "bug", "issue"))
        { positivity -= 0.25; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "annoying", "frustrating", "disappointing", "useless"))
        { positivity -= 0.35; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "no", "not", "don't", "can't", "won't", "shouldn't"))
        { positivity -= 0.1; matchCount++; }

        // === URGENCY DETECTION ===
        if (PersonalityHelpers.ContainsAny(lower, "urgent", "asap", "immediately", "right now", "emergency"))
        { urgency += 0.5; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "quick", "fast", "hurry", "soon", "deadline", "rush"))
        { urgency += 0.3; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "need", "must", "have to", "got to", "critical"))
        { urgency += 0.2; matchCount++; }

        // === CURIOSITY DETECTION ===
        if (input.Contains('?'))
        { curiosity += 0.3; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "why", "how", "what if", "wonder", "curious", "interested"))
        { curiosity += 0.35; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "explain", "tell me", "show me", "teach", "learn", "understand"))
        { curiosity += 0.25; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "explore", "discover", "investigate", "research", "dig into"))
        { curiosity += 0.3; matchCount++; }

        // === FRUSTRATION DETECTION ===
        if (PersonalityHelpers.ContainsAny(lower, "frustrated", "annoyed", "irritated", "angry", "mad"))
        { frustration += 0.5; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "doesn't work", "not working", "still broken", "again", "same problem"))
        { frustration += 0.4; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "ugh", "argh", "damn", "dammit", "seriously", "come on"))
        { frustration += 0.35; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "tried everything", "nothing works", "give up", "stuck"))
        { frustration += 0.45; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "why won't", "why doesn't", "why can't"))
        { frustration += 0.3; matchCount++; }

        // === ENGAGEMENT DETECTION ===
        // High engagement
        if (wordCount > 30) engagement += 0.2;
        if (wordCount > 50) engagement += 0.15;
        if (PersonalityHelpers.ContainsAny(lower, "specifically", "exactly", "precisely", "detail", "elaborate"))
        { engagement += 0.25; matchCount++; }
        if (PersonalityHelpers.ContainsAny(lower, "actually", "really", "truly", "genuinely"))
        { engagement += 0.15; matchCount++; }

        // Low engagement
        if (wordCount <= 3) engagement -= 0.2;
        if (PersonalityHelpers.ContainsAny(lower, "ok", "k", "sure", "fine", "whatever", "idc"))
        { engagement -= 0.3; matchCount++; }

        // === DETERMINE DOMINANT EMOTION ===
        string? dominantEmotion = null;
        double maxScore = 0;

        var emotions = new Dictionary<string, double>
        {
            ["excited"] = Math.Max(0, energy) + Math.Max(0, positivity) * 0.5,
            ["happy"] = Math.Max(0, positivity) * 0.8 + Math.Max(0, energy) * 0.2,
            ["curious"] = curiosity,
            ["frustrated"] = frustration,
            ["urgent"] = urgency,
            ["tired"] = Math.Max(0, -energy) * 0.7,
            ["sad"] = Math.Max(0, -positivity) * 0.5 + Math.Max(0, -energy) * 0.3,
            ["neutral"] = 0.3 - Math.Abs(energy) * 0.5 - Math.Abs(positivity) * 0.5
        };

        foreach (var (emotion, score) in emotions)
        {
            if (score > maxScore)
            {
                maxScore = score;
                dominantEmotion = emotion;
            }
        }

        // Calculate confidence based on match count and score magnitudes
        double confidence = Math.Min(1.0, 0.3 + (matchCount * 0.1) + (maxScore * 0.3));

        return new DetectedMood(
            Energy: Math.Clamp(energy, -1, 1),
            Positivity: Math.Clamp(positivity, -1, 1),
            Urgency: Math.Clamp(urgency, 0, 1),
            Curiosity: Math.Clamp(curiosity, 0, 1),
            Frustration: Math.Clamp(frustration, 0, 1),
            Engagement: Math.Clamp(engagement, 0, 1),
            DominantEmotion: dominantEmotion,
            Confidence: confidence);
    }

    /// <summary>
    /// Updates mood based on conversation dynamics.
    /// </summary>
    public void UpdateMood(string personaName, string userInput, bool positiveInteraction)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return;

        var currentMood = profile.CurrentMood;

        // Detect mood triggers from input
        double energyDelta = DetectEnergyChange(userInput);
        double positivityDelta = positiveInteraction ? 0.1 : -0.05;

        double newEnergy = Math.Clamp(currentMood.Energy + energyDelta, 0.0, 1.0);
        double newPositivity = Math.Clamp(currentMood.Positivity + positivityDelta, 0.0, 1.0);

        // Determine mood name based on energy/positivity
        string moodName = (newEnergy, newPositivity) switch
        {
            ( > 0.7, > 0.7) => "excited",
            ( > 0.7, < 0.3) => "intense",
            ( < 0.3, > 0.7) => "content",
            ( < 0.3, < 0.3) => "contemplative",
            (_, > 0.5) => "cheerful",
            _ => "focused"
        };

        // Get voice tone for the new mood
        var voiceTone = VoiceTone.ForMood(moodName);

        _profiles[personaName] = profile with
        {
            CurrentMood = new MoodState(moodName, newEnergy, newPositivity, currentMood.TraitModifiers, voiceTone)
        };
    }

    /// <summary>
    /// Updates mood based on comprehensive mood detection from user input.
    /// </summary>
    public void UpdateMoodFromDetection(string personaName, string userInput)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return;

        var detected = DetectMoodFromInput(userInput);
        var currentMood = profile.CurrentMood;

        // Blend detected mood with current mood (smooth transitions)
        double blendFactor = detected.Confidence * 0.4; // Higher confidence = more influence
        double newEnergy = Math.Clamp(
            currentMood.Energy + (detected.Energy * blendFactor),
            0.0, 1.0);
        double newPositivity = Math.Clamp(
            currentMood.Positivity + (detected.Positivity * blendFactor),
            0.0, 1.0);

        // Frustration reduces positivity and can increase energy (agitation)
        if (detected.Frustration > 0.3)
        {
            newPositivity = Math.Max(0.1, newPositivity - detected.Frustration * 0.3);
            newEnergy = Math.Min(1.0, newEnergy + detected.Frustration * 0.2);
        }

        // Curiosity increases engagement/energy slightly
        if (detected.Curiosity > 0.4)
        {
            newEnergy = Math.Min(1.0, newEnergy + 0.1);
        }

        // Urgency increases energy
        if (detected.Urgency > 0.3)
        {
            newEnergy = Math.Min(1.0, newEnergy + detected.Urgency * 0.2);
        }

        // Determine mood name with more nuanced detection
        string moodName = DetermineMoodName(newEnergy, newPositivity, detected);

        // Get voice tone for the new mood
        var voiceTone = VoiceTone.ForMood(moodName);

        _profiles[personaName] = profile with
        {
            CurrentMood = new MoodState(moodName, newEnergy, newPositivity, currentMood.TraitModifiers, voiceTone)
        };
    }

    /// <summary>
    /// Gets the current mood name for a persona.
    /// </summary>
    public string GetCurrentMood(string personaName)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return "neutral";

        return profile.CurrentMood.Name;
    }

    /// <summary>
    /// Gets the current voice tone settings for a persona.
    /// </summary>
    public VoiceTone GetVoiceTone(string personaName)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return VoiceTone.Neutral;

        return profile.CurrentMood.GetVoiceTone();
    }

    /// <summary>
    /// Determines mood name based on energy, positivity, and detected emotional cues.
    /// </summary>
    internal static string DetermineMoodName(double energy, double positivity, DetectedMood detected)
    {
        // Check for specific emotional states first
        if (detected.Frustration > 0.5)
            return "supportive"; // Respond with support when user is frustrated

        if (detected.Urgency > 0.5)
            return "focused"; // Match urgency with focus

        if (detected.Curiosity > 0.6)
            return "intrigued"; // Match curiosity

        // Use dominant emotion if confidence is high
        if (detected.Confidence > 0.6 && detected.DominantEmotion != null)
        {
            return detected.DominantEmotion switch
            {
                "excited" => "excited",
                "happy" => "cheerful",
                "curious" => "intrigued",
                "tired" => "calm",
                "sad" => "warm",
                "frustrated" => "supportive",
                _ => DetermineFromEnergyPositivity(energy, positivity)
            };
        }

        return DetermineFromEnergyPositivity(energy, positivity);
    }

    /// <summary>
    /// Determines mood name from energy and positivity values alone.
    /// </summary>
    internal static string DetermineFromEnergyPositivity(double energy, double positivity) =>
        (energy, positivity) switch
        {
            ( > 0.7, > 0.7) => "excited",
            ( > 0.7, < 0.3) => "intense",
            ( < 0.3, > 0.7) => "content",
            ( < 0.3, < 0.3) => "contemplative",
            (_, > 0.6) => "cheerful",
            (_, < 0.4) => "thoughtful",
            _ => "focused"
        };

    /// <summary>
    /// Detects energy change from user input keywords.
    /// </summary>
    internal static double DetectEnergyChange(string input)
    {
        string lower = input.ToLower();

        if (PersonalityHelpers.ContainsAny(lower, "exciting", "amazing", "awesome", "great", "love", "fantastic"))
            return 0.15;
        if (PersonalityHelpers.ContainsAny(lower, "boring", "tired", "slow", "meh", "whatever"))
            return -0.1;
        if (PersonalityHelpers.ContainsAny(lower, "urgent", "quick", "fast", "hurry", "asap"))
            return 0.1;

        return 0;
    }
}
