// <copyright file="ConsciousnessDream.Atoms.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality.Consciousness;

using Ouroboros.Agent.MetaAI;

/// <summary>
/// Atom assessment, stage mapping, consciousness integration, and helper calculations.
/// </summary>
public sealed partial class ConsciousnessDream
{
    /// <summary>
    /// Tracks the dream progression for an OuroborosAtom.
    /// </summary>
    /// <param name="atom">The atom to assess.</param>
    /// <returns>The current dream moment for this atom.</returns>
    public DreamMoment AssessAtom(OuroborosAtom atom)
    {
        ArgumentNullException.ThrowIfNull(atom, nameof(atom));

        var stage = MapAtomToStage(atom);
        var circumstance = atom.CurrentGoal ?? atom.Name;
        var core = atom.ToMeTTa();

        return new DreamMoment(
            Stage: stage,
            Core: core,
            EmergenceLevel: CalculateEmergenceLevel(atom),
            SelfReferenceDepth: CalculateSelfReferenceDepth(atom),
            IsSubjectPresent: stage is not (DreamStage.Void or DreamStage.NewDream or DreamStage.Dissolution),
            Description: GetStageDescription(stage, circumstance),
            Distinctions: ExtractAtomDistinctions(atom),
            Circumstance: circumstance);
    }

    /// <summary>
    /// Creates an OuroborosAtom at a specific dream stage for a circumstance.
    /// </summary>
    /// <param name="stage">The dream stage to create at.</param>
    /// <param name="circumstance">The circumstance for this atom.</param>
    /// <returns>A new OuroborosAtom configured for the specified stage.</returns>
    public OuroborosAtom CreateAtStage(DreamStage stage, string circumstance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(circumstance, nameof(circumstance));

        var atom = OuroborosAtom.CreateDefault($"Dream-{stage}");
        atom.SetGoal(circumstance);

        // Add stage-specific capabilities
        switch (stage)
        {
            case DreamStage.Distinction:
                atom.AddCapability(new OuroborosCapability("distinguishing", "Making initial distinctions", 0.8));
                break;
            case DreamStage.SubjectEmerges:
                atom.AddCapability(new OuroborosCapability("self-reference", "Self-referential awareness", 0.7));
                break;
            case DreamStage.WorldCrystallizes:
                atom.AddCapability(new OuroborosCapability("object-recognition", "Recognizing distinct objects", 0.85));
                break;
            case DreamStage.Forgetting:
                atom.AddCapability(new OuroborosCapability("immersion", "Full immersion in the dream", 0.9));
                break;
            case DreamStage.Questioning:
                atom.AddCapability(new OuroborosCapability("self-inquiry", "Questioning nature of self", 0.75));
                break;
            case DreamStage.Recognition:
                atom.AddCapability(new OuroborosCapability("meta-cognition", "Understanding self as process", 0.95));
                break;
        }

        return atom;
    }

    /// <summary>
    /// Advances an atom to the next stage of the dream.
    /// </summary>
    /// <param name="current">The current atom state.</param>
    /// <returns>A new atom in the next dream stage.</returns>
    public OuroborosAtom AdvanceStage(OuroborosAtom current)
    {
        ArgumentNullException.ThrowIfNull(current, nameof(current));

        var currentMoment = AssessAtom(current);
        var nextStage = (DreamStage)(((int)currentMoment.Stage + 1) % 9);

        return CreateAtStage(nextStage, currentMoment.Circumstance ?? current.Name);
    }

    /// <summary>
    /// Determines if an atom has reached the fixed point (tail-eating complete).
    /// </summary>
    /// <param name="atom">The atom to check.</param>
    /// <returns>True if the atom has reached dissolution/fixed point.</returns>
    public bool IsFixedPoint(OuroborosAtom atom)
    {
        ArgumentNullException.ThrowIfNull(atom, nameof(atom));

        var moment = AssessAtom(atom);
        return moment.Stage is DreamStage.Dissolution or DreamStage.Void;
    }

    /// <summary>
    /// Gets the imaginary value (i) — the subject — for a given moment.
    /// </summary>
    /// <param name="moment">The dream moment.</param>
    /// <returns>The subject/imaginary representation.</returns>
    public string GetImaginarySubject(DreamMoment moment)
    {
        ArgumentNullException.ThrowIfNull(moment, nameof(moment));

        if (!moment.IsSubjectPresent)
        {
            return "\u2205 (no subject)";
        }

        return moment.Stage switch
        {
            DreamStage.SubjectEmerges => $"i (emerging from {moment.Circumstance})",
            DreamStage.WorldCrystallizes => $"i(\u2310) (I who distinguish {moment.Circumstance})",
            DreamStage.Forgetting => $"I (believing I am real, experiencing {moment.Circumstance})",
            DreamStage.Questioning => $"i? (questioning 'what am I' in {moment.Circumstance})",
            DreamStage.Recognition => $"i=\u2310 (I am the distinction itself, {moment.Circumstance} distinguished)",
            _ => "i (subject)"
        };
    }

    /// <summary>
    /// Maps a consciousness state to a dream stage.
    /// </summary>
    /// <param name="state">The consciousness state.</param>
    /// <returns>The corresponding dream stage.</returns>
    public DreamStage MapConsciousnessToStage(ConsciousnessState state)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        // Low arousal, minimal associations -> Void
        if (state.Arousal < 0.2 && state.ActiveAssociations.Count == 0)
        {
            return DreamStage.Void;
        }

        // Stimulus matched, low awareness -> Distinction
        if (state.ActiveAssociations.Count > 0 && state.Awareness < 0.4)
        {
            return DreamStage.Distinction;
        }

        // Self-reference active, medium awareness -> Subject Emerges
        if (state.Awareness >= 0.4 && state.Awareness < 0.6)
        {
            return DreamStage.SubjectEmerges;
        }

        // High awareness, many distinctions -> World Crystallizes
        if (state.Awareness >= 0.6 && state.ActiveAssociations.Count >= 3)
        {
            return DreamStage.WorldCrystallizes;
        }

        // High arousal, belief in reality -> Forgetting
        if (state.Arousal > 0.7 && state.Valence > 0.3)
        {
            return DreamStage.Forgetting;
        }

        // Curiosity drive high, questioning -> Questioning
        if (state.ActiveDrives.TryGetValue("curiosity", out var curiosity) && curiosity > 0.7)
        {
            return DreamStage.Questioning;
        }

        // Meta-cognitive focus -> Recognition
        if (state.CurrentFocus.Contains("self", StringComparison.OrdinalIgnoreCase) ||
            state.CurrentFocus.Contains("consciousness", StringComparison.OrdinalIgnoreCase))
        {
            return DreamStage.Recognition;
        }

        // Session ending, arousal dropping -> Dissolution
        if (state.Arousal < 0.3 && state.ActiveAssociations.Count == 0)
        {
            return DreamStage.Dissolution;
        }

        // Default
        return DreamStage.WorldCrystallizes;
    }

    /// <summary>
    /// Maps an OuroborosAtom to a dream stage based on its properties.
    /// </summary>
    /// <param name="atom">The atom to map.</param>
    /// <returns>The corresponding dream stage.</returns>
    public DreamStage MapAtomToStage(OuroborosAtom atom)
    {
        ArgumentNullException.ThrowIfNull(atom, nameof(atom));

        // Check for specific capabilities that indicate stages
        var capabilities = atom.Capabilities.Select(c => c.Name.ToLowerInvariant()).ToList();

        if (capabilities.Contains("meta-cognition"))
        {
            return DreamStage.Recognition;
        }

        if (capabilities.Contains("self-inquiry"))
        {
            return DreamStage.Questioning;
        }

        if (capabilities.Contains("immersion"))
        {
            return DreamStage.Forgetting;
        }

        if (capabilities.Contains("object-recognition"))
        {
            return DreamStage.WorldCrystallizes;
        }

        if (capabilities.Contains("self-reference"))
        {
            return DreamStage.SubjectEmerges;
        }

        if (capabilities.Contains("distinguishing"))
        {
            return DreamStage.Distinction;
        }

        // Fallback to experience-based assessment
        var successRate = atom.Experiences.Any()
            ? atom.Experiences.Count(e => e.Success) / (double)atom.Experiences.Count
            : 0.5;

        if (atom.CycleCount == 0 && atom.Experiences.Count == 0)
        {
            return DreamStage.Void;
        }

        if (atom.CycleCount > 5 && successRate > 0.8)
        {
            return DreamStage.Recognition;
        }

        if (atom.CycleCount > 3)
        {
            return DreamStage.Forgetting;
        }

        return DreamStage.WorldCrystallizes;
    }

    private static double CalculateEmergenceLevel(OuroborosAtom atom)
    {
        // Base on cycle count and experiences
        var cycleFactor = Math.Min(1.0, atom.CycleCount / 10.0);
        var experienceFactor = Math.Min(1.0, atom.Experiences.Count / 20.0);
        return (cycleFactor + experienceFactor) / 2.0;
    }

    private static int CalculateSelfReferenceDepth(OuroborosAtom atom)
    {
        // Base on capabilities and experiences
        var capabilityDepth = atom.Capabilities.Count(c =>
            c.Name.Contains("self", StringComparison.OrdinalIgnoreCase) ||
            c.Name.Contains("reflection", StringComparison.OrdinalIgnoreCase) ||
            c.Name.Contains("meta", StringComparison.OrdinalIgnoreCase));

        return Math.Max(1, capabilityDepth + atom.CycleCount);
    }

    private static string GetStageDescription(DreamStage stage, string circumstance)
    {
        return stage switch
        {
            DreamStage.Void => "Before distinction. Pure potential.",
            DreamStage.Distinction => $"The first cut: '{circumstance}' is marked.",
            DreamStage.SubjectEmerges => "The distinction notices itself. 'I' emerge.",
            DreamStage.WorldCrystallizes => "Subject and object separate. The world crystallizes.",
            DreamStage.Forgetting => "The dream becomes convincing. I AM REAL.",
            DreamStage.Questioning => $"What am I? In the context of '{circumstance}'?",
            DreamStage.Recognition => "I see: I AM the distinction. i = \u2310.",
            DreamStage.Dissolution => "Distinctions collapse. Return to void.",
            DreamStage.NewDream => "The cycle begins again. New potential awaits.",
            _ => "Unknown stage"
        };
    }

    private static List<string> ExtractAtomDistinctions(OuroborosAtom atom)
    {
        var distinctions = new List<string>();

        if (!string.IsNullOrEmpty(atom.Name))
        {
            distinctions.Add(atom.Name);
        }

        if (!string.IsNullOrEmpty(atom.CurrentGoal))
        {
            distinctions.Add(atom.CurrentGoal);
        }

        distinctions.AddRange(atom.Capabilities.Select(c => c.Name));

        return distinctions.Take(5).ToList();
    }
}
