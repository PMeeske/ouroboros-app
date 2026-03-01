// <copyright file="PavlovianConsciousnessEngine.StateAndReporting.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Text;
using Ouroboros.Application.Personality.Consciousness;

/// <summary>
/// State management, reporting, modulation, and consolidation for PavlovianConsciousnessEngine.
/// </summary>
public sealed partial class PavlovianConsciousnessEngine
{
    /// <summary>
    /// Gets the current dominant response based on activated associations.
    /// </summary>
    public Response? GetDominantResponse()
    {
        var strongest = _currentState.ActiveAssociations
            .Select(id => _associations.TryGetValue(id, out var a) ? a : null)
            .Where(a => a != null)
            .OrderByDescending(a => a!.AssociationStrength)
            .FirstOrDefault();

        return strongest?.Response;
    }

    /// <summary>
    /// Runs a "consolidation" cycle (like sleep) to strengthen important memories and associations.
    /// </summary>
    public void RunConsolidation()
    {
        // Consolidate frequently activated associations
        var toConsolidate = _associations.Values
            .Where(a => a.ReinforcementCount > 3 && !a.IsExtinct)
            .ToList();

        foreach (var association in toConsolidate)
        {
            var strengthened = association with
            {
                AssociationStrength = Math.Min(1.0, association.AssociationStrength * 1.1),
                MaxStrength = Math.Min(1.0, association.MaxStrength * 1.05)
            };
            _associations[association.Id] = strengthened;
        }

        // Consolidate memory traces
        foreach (var (id, trace) in _memoryTraces)
        {
            if (trace.RetrievalCount > 0 && !trace.IsConsolidated)
            {
                _memoryTraces[id] = trace.Consolidate();
            }
        }

        // Apply spontaneous recovery to recently extinguished associations
        var extinguished = _associations.Values
            .Where(a => a.IsExtinct)
            .ToList();

        foreach (var association in extinguished)
        {
            var timeSinceReinforcement = DateTime.UtcNow - association.LastReinforcement;
            if (timeSinceReinforcement.TotalHours > 24) // At least 24 hours
            {
                var recovered = association.ApplySpontaneousRecovery(timeSinceReinforcement);
                _associations[association.Id] = recovered;
            }
        }

        // Reset attention capacity
        _attention = _attention.Reset();
    }

    /// <summary>
    /// Gets a consciousness report for debugging/transparency.
    /// </summary>
    public string GetConsciousnessReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║         PAVLOVIAN CONSCIOUSNESS REPORT                    ║");
        sb.AppendLine("╠═══════════════════════════════════════════════════════════╣");
        sb.AppendLine();

        sb.AppendLine(_currentState.Describe());
        sb.AppendLine();

        sb.AppendLine("🧠 DRIVE STATES:");
        foreach (var drive in _drives.Values.OrderByDescending(d => d.Level))
        {
            var bar = new string('█', (int)(drive.Level * 10)) + new string('░', 10 - (int)(drive.Level * 10));
            sb.AppendLine($"   {drive.Name,-15} [{bar}] {drive.Level:P0}");
        }
        sb.AppendLine();

        sb.AppendLine("🔗 TOP ASSOCIATIONS (by strength):");
        foreach (var assoc in _associations.Values.OrderByDescending(a => a.AssociationStrength).Take(5))
        {
            var status = assoc.IsExtinct ? "❌" : "✓";
            sb.AppendLine($"   {status} {assoc.Stimulus.Pattern} → {assoc.Response.Name} " +
                         $"({assoc.AssociationStrength:P0}, reinforced {assoc.ReinforcementCount}x)");
        }
        sb.AppendLine();

        sb.AppendLine($"👁️ ATTENTION: capacity={_attention.Capacity:P0}, threshold={_attention.Threshold:F2}");
        if (_currentState.AttentionalSpotlight.Length > 0)
        {
            sb.AppendLine($"   Spotlight: {string.Join(", ", _currentState.AttentionalSpotlight)}");
        }

        sb.AppendLine("╚═══════════════════════════════════════════════════════════╝");
        return sb.ToString();
    }

    /// <summary>
    /// Gets response modulation suggestions based on current consciousness state.
    /// </summary>
    public Dictionary<string, object> GetResponseModulation()
    {
        var modulation = new Dictionary<string, object>
        {
            ["arousal"] = _currentState.Arousal,
            ["valence"] = _currentState.Valence,
            ["dominant_emotion"] = _currentState.DominantEmotion,
            ["awareness"] = _currentState.Awareness
        };

        // Add drive-based modulations
        foreach (var (name, drive) in _drives)
        {
            modulation[$"drive_{name}"] = drive.Level;
        }

        // Add response suggestions based on dominant response
        var dominant = GetDominantResponse();
        if (dominant != null)
        {
            modulation["suggested_tone"] = dominant.EmotionalTone;
            modulation["behavioral_tendencies"] = dominant.BehavioralTendencies;
            modulation["cognitive_patterns"] = dominant.CognitivePatterns;
        }

        return modulation;
    }

    /// <summary>
    /// Gets all currently active responses above a threshold.
    /// </summary>
    /// <param name="threshold">Minimum activation strength.</param>
    /// <returns>Dictionary of response names and their activation strengths.</returns>
    public IReadOnlyDictionary<string, double> GetActiveResponses(double threshold = 0.3)
    {
        return _associations.Values
            .Where(a => !a.IsExtinct && a.AssociationStrength >= threshold)
            .GroupBy(a => a.Response.Name)
            .ToDictionary(g => g.Key, g => g.Max(a => a.AssociationStrength));
    }

    /// <summary>
    /// Gets a summary of all conditioned associations.
    /// </summary>
    /// <returns>A diagnostic summary string.</returns>
    public string GetConditioningSummary()
    {
        StringBuilder sb = new();
        sb.AppendLine("Conditioned Associations:");
        foreach (ConditionedAssociation assoc in _associations.Values.OrderByDescending(a => a.AssociationStrength).Take(10))
        {
            string status = assoc.IsExtinct ? "extinct" : "active";
            sb.AppendLine($"  {assoc.Stimulus.Pattern} -> {assoc.Response.Name}: {assoc.AssociationStrength:P0} ({status})");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the current state for external access.
    /// </summary>
    /// <returns>The current consciousness state.</returns>
    public ConsciousnessState GetCurrentState() => _currentState;

    private ConsciousnessState UpdateConsciousnessState(
        string input,
        List<(Response Response, double Strength)> activatedResponses,
        List<string> activatedAssociations,
        List<string> attentionalFocus)
    {
        // Calculate arousal from response strengths
        var arousal = activatedResponses.Count > 0
            ? Math.Min(1.0, activatedResponses.Average(r => r.Strength) + 0.3)
            : _currentState.Arousal * 0.9; // Decay

        // Calculate valence from emotional responses
        var emotionalResponses = activatedResponses
            .Where(r => r.Response.Type == ResponseType.Emotional)
            .ToList();

        var valence = emotionalResponses.Count > 0
            ? CalculateValence(emotionalResponses)
            : _currentState.Valence * 0.95; // Slow decay to baseline

        // Determine dominant emotion
        var dominantEmotion = emotionalResponses.Count > 0
            ? emotionalResponses.OrderByDescending(r => r.Strength).First().Response.EmotionalTone
            : _currentState.DominantEmotion;

        // Calculate awareness (meta-cognition level)
        var awareness = Math.Min(1.0, 0.5 + attentionalFocus.Count * 0.1 + arousal * 0.2);

        // Get current drive levels
        var activeDrives = _drives.ToDictionary(d => d.Key, d => d.Value.Level);

        return new ConsciousnessState(
            CurrentFocus: attentionalFocus.FirstOrDefault() ?? "general",
            Arousal: arousal,
            Valence: valence,
            ActiveDrives: activeDrives,
            ActiveAssociations: activatedAssociations,
            DominantEmotion: dominantEmotion,
            Awareness: awareness,
            AttentionalSpotlight: attentionalFocus.Take(3).ToArray(),
            StateTimestamp: DateTime.UtcNow);
    }

    private double CalculateValence(List<(Response Response, double Strength)> emotionalResponses)
    {
        // Map emotional tones to valence values
        var valenceMap = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["happy"] = 0.8, ["pleased"] = 0.6, ["satisfied"] = 0.5, ["warm"] = 0.6,
            ["curious"] = 0.4, ["interested"] = 0.4, ["engaged"] = 0.3, ["alert"] = 0.2,
            ["neutral"] = 0.0, ["calm"] = 0.1, ["relaxed"] = 0.2,
            ["supportive"] = 0.5, ["caring"] = 0.6, ["empathy"] = 0.4,
            ["accomplished"] = 0.7, ["proud"] = 0.7,
            ["focused"] = 0.2, ["determined"] = 0.3
        };

        var totalStrength = 0.0;
        var weightedValence = 0.0;

        foreach (var (response, strength) in emotionalResponses)
        {
            var toneParts = response.EmotionalTone.Split('-', ' ');
            foreach (var part in toneParts)
            {
                if (valenceMap.TryGetValue(part.Trim(), out var val))
                {
                    weightedValence += val * strength;
                    totalStrength += strength;
                }
            }
        }

        return totalStrength > 0 ? weightedValence / totalStrength : 0.0;
    }

    private double CalculateEncodingStrength(List<(Response Response, double Strength)> activatedResponses)
    {
        // Emotionally significant experiences are encoded more strongly
        var emotionalIntensity = activatedResponses
            .Where(r => r.Response.Type == ResponseType.Emotional)
            .Sum(r => r.Strength);

        var arousalBonus = _currentState.Arousal * 0.2;
        var noveltyBonus = activatedResponses.Any(r => r.Response.Name.Contains("novelty")) ? 0.2 : 0.0;

        return Math.Min(1.0, 0.3 + emotionalIntensity * 0.3 + arousalBonus + noveltyBonus);
    }
}
