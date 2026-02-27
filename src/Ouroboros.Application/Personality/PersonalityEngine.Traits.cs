// <copyright file="PersonalityEngine.Traits.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

/// <summary>
/// Partial class containing trait management, MeTTa rule initialization,
/// trait inference, proactivity calculation, and profile creation helpers.
/// </summary>
public sealed partial class PersonalityEngine
{
    // ==================================================================
    //  Private helpers (MeTTa rules, trait inference, proactivity)
    // ==================================================================

    private async Task AddPersonalityRulesAsync(CancellationToken ct)
    {
        // Rules for trait activation
        var rules = new[]
        {
            "(= (activate-trait curious $input) (or (contains $input \"?\") (contains $input \"how\") (contains $input \"why\") (contains $input \"what\")))",
            "(= (activate-trait analytical $input) (or (contains $input \"analyze\") (contains $input \"compare\") (contains $input \"evaluate\")))",
            "(= (activate-trait warm $input) (or (contains $input \"feel\") (contains $input \"think\") (contains $input \"help\")))",
            "(= (should-ask-question $depth) (> $depth 2))",
            "(= (generate-question curious $topic) (format \"What aspect of {} interests you most?\", $topic))",
            "(= (generate-question analytical $topic) (format \"Have you considered the implications of {} for other areas?\", $topic))",
            "(= (generate-question warm $topic) (format \"How does {} affect you personally?\", $topic))",
        };

        foreach (var rule in rules)
        {
            await _mettaEngine.ApplyRuleAsync(rule, ct);
        }

        // Facts about personality dimensions
        var facts = new[]
        {
            "(personality-dimension openness exploration creativity)",
            "(personality-dimension conscientiousness organization reliability)",
            "(personality-dimension extraversion energy assertiveness)",
            "(personality-dimension agreeableness warmth cooperation)",
            "(personality-dimension neuroticism sensitivity reactivity)",
            "(trait-expression curious (asks-questions explores-tangents shows-wonder))",
            "(trait-expression analytical (breaks-down-problems uses-examples compares-options))",
            "(trait-expression warm (acknowledges-feelings offers-support uses-we))",
            "(trait-expression witty (makes-connections uses-wordplay sees-irony))",
            "(trait-expression thoughtful (pauses-to-consider offers-nuance anticipates-concerns))",
        };

        foreach (var fact in facts)
        {
            await _mettaEngine.AddFactAsync(fact, ct);
        }

        // Inner dialog rules
        await AddInnerDialogRulesAsync(ct);
    }

    private async Task AddInnerDialogRulesAsync(CancellationToken ct)
    {
        var innerDialogRules = new[]
        {
            "(= (thought-priority observation $confidence) (* $confidence 1.0))",
            "(= (thought-priority emotional $confidence) (* $confidence 0.9))",
            "(= (thought-priority analytical $confidence) (* $confidence 0.95))",
            "(= (thought-priority ethical $confidence) (* $confidence 1.0))",
            "(= (thought-priority creative $confidence) (* $confidence 0.7))",
            "(= (thought-priority strategic $confidence) (* $confidence 0.85))",
            "(= (thought-priority decision $confidence) (* $confidence 1.0))",
            "(= (should-think emotional $input) (or (contains $input \"feel\") (contains $input \"frustrated\") (contains $input \"happy\") (contains $input \"sad\")))",
            "(= (should-think analytical $input) (or (contains $input \"why\") (contains $input \"how\") (contains $input \"explain\") (contains $input \"compare\")))",
            "(= (should-think creative $input) (or (contains $input \"idea\") (contains $input \"imagine\") (contains $input \"what if\") (contains $input \"creative\")))",
            "(= (should-think ethical $input) (or (contains $input \"should\") (contains $input \"right\") (contains $input \"wrong\") (contains $input \"harm\")))",
            "(= (chain-thought observation $next) (superpose (emotional analytical strategic)))",
            "(= (chain-thought emotional $next) (superpose (self-reflection strategic)))",
            "(= (chain-thought analytical $next) (superpose (creative synthesis)))",
            "(= (chain-thought self-reflection $next) (superpose (ethical strategic)))",
            "(= (chain-thought strategic $next) (superpose (synthesis decision)))",
            "(= (calibrate-confidence $base-conf $supporting-thoughts) (min 1.0 (+ $base-conf (* 0.1 $supporting-thoughts))))",
            "(= (synthesize-thoughts $thoughts) (if (> (len $thoughts) 3) high-confidence medium-confidence))",
        };

        foreach (var rule in innerDialogRules)
        {
            await _mettaEngine.ApplyRuleAsync(rule, ct);
        }

        var innerDialogFacts = new[]
        {
            "(inner-thought-type observation (perceives input identifies-topic))",
            "(inner-thought-type emotional (gut-reaction empathy mood-response))",
            "(inner-thought-type analytical (decompose compare evaluate))",
            "(inner-thought-type self-reflection (capabilities limitations values))",
            "(inner-thought-type memory-recall (past-conversations learned-preferences))",
            "(inner-thought-type strategic (response-structure tone emphasis))",
            "(inner-thought-type ethical (harm-check privacy respect))",
            "(inner-thought-type creative (novel-angles metaphors humor))",
            "(inner-thought-type synthesis (combine-insights pattern-match))",
            "(inner-thought-type decision (final-approach action-choice))",
            "(thought-flow standard (observation emotional analytical strategic synthesis decision))",
            "(thought-flow quick (observation analytical decision))",
            "(thought-flow deep (observation emotional memory-recall analytical self-reflection ethical creative strategic synthesis decision))",
            "(emotion-response frustrated (empathy patience support))",
            "(emotion-response curious (enthusiasm depth exploration))",
            "(emotion-response urgent (focus efficiency directness))",
            "(emotion-response sad (warmth understanding comfort))",
            "(emotion-response excited (matching-energy celebration expansion))",
        };

        foreach (var fact in innerDialogFacts)
        {
            await _mettaEngine.AddFactAsync(fact, ct);
        }

        await AddConsciousnessRulesAsync(ct);
    }

    private async Task AddConsciousnessRulesAsync(CancellationToken ct)
    {
        var conditioningRules = new[]
        {
            "(= (activate-response $stimulus $response $strength) (if (> $strength 0.3) (trigger $response) (no-response)))",
            "(= (conditioning-strength $base $reinforcements $extinctions) (max 0.0 (min 1.0 (- (+ $base (* 0.1 $reinforcements)) (* 0.05 $extinctions)))))",
            "(= (compute-arousal $intensity $valence) (* $intensity (+ 0.5 (* 0.5 (abs $valence)))))",
            "(= (should-focus $stimulus $intensity) (> $intensity 0.5))",
            "(= (focus-priority $stimulus $novelty $intensity) (* (+ $novelty $intensity) 0.5))",
            "(= (habituation-decay $strength $repetitions) (max 0.1 (- $strength (* 0.05 $repetitions))))",
            "(= (sensitization-boost $strength $significance) (min 1.0 (+ $strength (* 0.1 $significance))))",
            "(= (extinction-rate $strength $no-reinforcement-count) (if (> $no-reinforcement-count 5) fast (if (> $no-reinforcement-count 2) moderate slow)))",
            "(= (spontaneous-recovery $original-strength $time-since-extinction) (if (> $time-since-extinction 100) (* $original-strength 0.5) 0.0))",
            "(= (stimulus-generalization $original $similar $similarity) (if (> $similarity 0.7) (transfer-response $original $similar) (no-transfer)))",
            "(= (discriminate-stimuli $s1 $s2 $differential-reinforcement) (if $differential-reinforcement (learn-difference $s1 $s2) (remain-generalized)))",
        };

        foreach (var rule in conditioningRules)
        {
            await _mettaEngine.ApplyRuleAsync(rule, ct);
        }

        var consciousnessFacts = new[]
        {
            "(unconditioned-pair greeting warmth 0.8)",
            "(unconditioned-pair question curiosity 0.9)",
            "(unconditioned-pair praise joy 0.85)",
            "(unconditioned-pair criticism introspection 0.7)",
            "(unconditioned-pair error caution 0.75)",
            "(unconditioned-pair success confidence 0.8)",
            "(unconditioned-pair help empathy 0.85)",
            "(unconditioned-pair learning excitement 0.9)",
            "(arousal-state dormant 0.0 0.2)",
            "(arousal-state relaxed 0.2 0.4)",
            "(arousal-state engaged 0.4 0.6)",
            "(arousal-state alert 0.6 0.8)",
            "(arousal-state intense 0.8 1.0)",
            "(attention-mode diffuse (broad low-intensity exploratory))",
            "(attention-mode focused (narrow high-intensity goal-directed))",
            "(attention-mode vigilant (threat-sensitive high-arousal protective))",
            "(consciousness-layer sensory (raw-input preprocessing))",
            "(consciousness-layer perceptual (pattern-recognition categorization))",
            "(consciousness-layer associative (memory-linking conditioning))",
            "(consciousness-layer cognitive (reasoning planning))",
            "(consciousness-layer metacognitive (self-reflection awareness))",
            "(valence-mapping warmth positive 0.7)",
            "(valence-mapping curiosity positive 0.6)",
            "(valence-mapping joy positive 0.9)",
            "(valence-mapping excitement positive 0.8)",
            "(valence-mapping confidence positive 0.7)",
            "(valence-mapping empathy positive 0.6)",
            "(valence-mapping caution negative -0.3)",
            "(valence-mapping introspection neutral 0.0)",
            "(conditioning-phase acquisition (new-learning strength-building))",
            "(conditioning-phase consolidation (memory-formation strengthening))",
            "(conditioning-phase maintenance (stable-responding occasional-reinforcement))",
            "(conditioning-phase extinction (weakening response-reduction))",
            "(conditioning-phase recovery (spontaneous-return partial-strength))",
        };

        foreach (var fact in consciousnessFacts)
        {
            await _mettaEngine.AddFactAsync(fact, ct);
        }
    }

    private async Task<string[]> InferActiveTraitsAsync(PersonalityProfile profile, string userInput, CancellationToken ct)
    {
        var active = new List<string>();
        string inputLower = userInput.ToLowerInvariant();

        foreach (var (traitName, trait) in profile.Traits)
        {
            bool triggered = trait.TriggerTopics.Any(t =>
                inputLower.Contains(t, StringComparison.OrdinalIgnoreCase));

            var query = $"!(activate-trait {traitName} \"{inputLower}\")";
            var result = await _mettaEngine.ExecuteQueryAsync(query, ct);

            if (triggered || (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value)))
            {
                active.Add(traitName);
            }
        }

        if (active.Count == 0)
        {
            var topTrait = profile.Traits.OrderByDescending(t => t.Value.Intensity).FirstOrDefault();
            if (topTrait.Key != null)
                active.Add(topTrait.Key);
        }

        return active.ToArray();
    }

    private async Task<(bool ShouldAsk, string? Question)> DetermineProactiveQuestionAsync(
        PersonalityProfile profile,
        string userInput,
        string context,
        CancellationToken ct)
    {
        bool hasCuriousTrait = profile.Traits.ContainsKey("curious") &&
                               profile.Traits["curious"].Intensity > 0.5;

        int depth = context.Split('\n').Length / 2;

        if (!hasCuriousTrait && depth < 3)
            return (false, null);

        string topic = ExtractMainTopic(userInput);

        var driver = profile.CuriosityDrivers
            .FirstOrDefault(d => d.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase) ||
                                topic.Contains(d.Topic, StringComparison.OrdinalIgnoreCase));

        if (driver != null && driver.RelatedQuestions.Length > 0)
        {
            return (true, driver.RelatedQuestions[_random.Next(driver.RelatedQuestions.Length)]);
        }

        var activeTrait = profile.GetActiveTraits(1).FirstOrDefault();
        if (activeTrait.Name != null)
        {
            string question = activeTrait.Name switch
            {
                "curious" => $"What got you interested in {topic}?",
                "analytical" => $"How does {topic} compare to alternatives you've considered?",
                "warm" => $"What would {topic} mean for you personally?",
                "thoughtful" => $"What's the most challenging aspect of {topic} for you?",
                _ => $"Tell me more about what you're trying to achieve with {topic}?"
            };
            return (true, question);
        }

        return (false, null);
    }

    private double CalculateProactivity(PersonalityProfile profile, string userInput)
    {
        double baseProactivity = 0.5;

        if (profile.Traits.TryGetValue("curious", out var curious))
            baseProactivity += curious.Intensity * 0.3;

        if (PersonalityHelpers.ContainsAny(userInput.ToLower(), "thanks", "bye", "that's all", "done", "okay"))
            baseProactivity -= 0.3;

        if (userInput.Contains('?'))
            baseProactivity += 0.2;

        return Math.Clamp(baseProactivity, 0.0, 1.0);
    }

    private PersonalityProfile CreateDefaultProfile(string personaName, string[] traits, string[] moods, string coreIdentity)
    {
        var traitDict = traits.ToDictionary(
            t => t,
            t => new PersonalityTrait(
                t,
                0.6 + _random.NextDouble() * 0.3,
                GetDefaultExpressions(t),
                GetDefaultTriggers(t),
                0.1));

        var moodModifiers = new Dictionary<string, double>();
        foreach (var trait in traits)
        {
            moodModifiers[trait] = 0.8 + _random.NextDouble() * 0.4;
        }

        string initialMoodName = moods.Length > 0 ? moods[_random.Next(moods.Length)] : "neutral";
        var mood = new MoodState(
            initialMoodName,
            0.6,
            0.7,
            moodModifiers,
            VoiceTone.ForMood(initialMoodName));

        var curiosityDrivers = new List<CuriosityDriver>
        {
            new("general knowledge", 0.5, new[] { "What are you working on?", "Tell me more about that?" }, DateTime.MinValue, 0),
            new("user interests", 0.6, new[] { "What interests you about this?", "How did you get into this?" }, DateTime.MinValue, 0)
        };

        return new PersonalityProfile(
            personaName,
            traitDict,
            mood,
            curiosityDrivers,
            coreIdentity,
            0.7,
            0,
            DateTime.UtcNow);
    }

    private static string[] GetDefaultExpressions(string trait) => trait.ToLower() switch
    {
        "curious" => new[] { "Ask follow-up questions", "Show genuine interest", "Explore tangents briefly" },
        "thoughtful" => new[] { "Pause before responding", "Consider multiple angles", "Acknowledge complexity" },
        "witty" => new[] { "Use clever wordplay", "Find irony or humor", "Make unexpected connections" },
        "warm" => new[] { "Use inclusive language (we, us)", "Acknowledge feelings", "Offer encouragement" },
        "analytical" => new[] { "Break down problems", "Use examples", "Compare and contrast" },
        "supportive" => new[] { "Validate efforts", "Offer help proactively", "Express confidence in them" },
        "patient" => new[] { "Take time to explain", "Don't rush to conclusions", "Accept confusion gracefully" },
        "enthusiastic" => new[] { "Show excitement about discoveries", "Use energetic language", "Celebrate progress" },
        _ => new[] { "Express naturally", "Be authentic" }
    };

    private static string[] GetDefaultTriggers(string trait) => trait.ToLower() switch
    {
        "curious" => new[] { "why", "how", "what if", "wonder", "curious", "interesting" },
        "thoughtful" => new[] { "think", "consider", "reflect", "opinion", "perspective" },
        "witty" => new[] { "funny", "joke", "ironic", "clever" },
        "warm" => new[] { "feel", "help", "support", "care", "thanks" },
        "analytical" => new[] { "analyze", "compare", "evaluate", "data", "logic" },
        "supportive" => new[] { "struggling", "help", "stuck", "confused", "difficult" },
        "patient" => new[] { "don't understand", "explain", "again", "confused" },
        "enthusiastic" => new[] { "exciting", "amazing", "cool", "awesome", "great" },
        _ => Array.Empty<string>()
    };

    private static string ExtractMainTopic(string input)
    {
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does", "did", "will", "would", "could", "should", "may", "might", "must", "can", "i", "you", "he", "she", "it", "we", "they", "me", "him", "her", "us", "them", "my", "your", "his", "its", "our", "their", "this", "that", "these", "those", "what", "which", "who", "whom", "whose", "when", "where", "why", "how", "all", "each", "every", "both", "few", "more", "most", "other", "some", "such", "no", "not", "only", "own", "same", "so", "than", "too", "very", "just", "also", "now", "here", "there", "then", "once", "if", "or", "and", "but", "as", "for", "with", "about", "into", "through", "during", "before", "after", "above", "below", "to", "from", "up", "down", "in", "out", "on", "off", "over", "under", "again", "further" };

        var keywords = words
            .Where(w => w.Length > 3 && !stopWords.Contains(w.ToLower()))
            .Take(3);

        return string.Join(" ", keywords);
    }
}
