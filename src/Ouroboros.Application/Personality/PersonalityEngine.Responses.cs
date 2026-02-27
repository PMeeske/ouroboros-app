// <copyright file="PersonalityEngine.Responses.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Text;

/// <summary>
/// Partial class containing greeting generation, courtesy responses,
/// proactive questions, and response modifier logic.
/// </summary>
public sealed partial class PersonalityEngine
{
    /// <summary>
    /// Gets a greeting personalized for the detected person.
    /// </summary>
    public string GetPersonalizedGreeting()
    {
        if (_personDetectionEngine.CurrentPerson == null)
            return "Hello! How can I help you today?";

        var person = _personDetectionEngine.CurrentPerson;
        var name = person.Name ?? "there";
        var isReturning = person.InteractionCount > 1;
        var relationship = _relationshipManager.GetRelationship(person.Id);

        // Add courtesy prefix based on relationship
        var courtesyPrefix = "";
        if (relationship != null && relationship.Rapport > 0.5)
        {
            courtesyPrefix = _relationshipManager.GetCourtesyPrefix(person.Id);
        }

        if (isReturning && person.Name != null)
        {
            var lastSeen = DateTime.UtcNow - person.LastSeen;
            if (lastSeen.TotalHours < 1)
            {
                var warmth = relationship != null && relationship.Rapport > 0.7
                    ? $"I was just thinking about our last conversation. "
                    : "";
                return $"{courtesyPrefix}Welcome back, {name}! {warmth}Continuing where we left off?";
            }
            if (lastSeen.TotalDays < 1)
                return $"{courtesyPrefix}Hi again, {name}! Good to see you back.";
            if (lastSeen.TotalDays < 7)
            {
                var sharedTopic = relationship?.SharedTopics.LastOrDefault();
                var topicReminder = sharedTopic != null
                    ? $" Last time we discussed {sharedTopic}."
                    : "";
                return $"{courtesyPrefix}Hello, {name}! It's been a few days.{topicReminder} How have you been?";
            }
            return $"{courtesyPrefix}Welcome back, {name}! It's been a while. Great to see you again!";
        }

        return person.Name != null
            ? $"Nice to meet you, {name}! I'm {_selfAwareness.Name}. How can I help you today?"
            : "Hello! What can I help you with today?";
    }

    /// <summary>
    /// Generates a proactive question based on personality and context.
    /// </summary>
    public async Task<string?> GenerateProactiveQuestionAsync(
        string personaName,
        string currentTopic,
        string[] conversationHistory,
        CancellationToken ct = default)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return null;

        // Find relevant curiosity drivers
        var relevantDrivers = profile.CuriosityDrivers
            .Where(d => d.CanAskAgain(TimeSpan.FromMinutes(5)) &&
                       (d.Topic.Contains(currentTopic, StringComparison.OrdinalIgnoreCase) ||
                        currentTopic.Contains(d.Topic, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(d => d.Interest)
            .ToList();

        if (relevantDrivers.Count == 0)
        {
            // Generate new curiosity based on topic
            return GenerateNewCuriosity(profile, currentTopic);
        }

        var driver = relevantDrivers.First();
        if (driver.RelatedQuestions.Length > 0)
        {
            int idx = _random.Next(driver.RelatedQuestions.Length);
            return driver.RelatedQuestions[idx];
        }

        return null;
    }

    /// <summary>
    /// Gets personality-influenced response modifiers.
    /// </summary>
    public string GetResponseModifiers(string personaName)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return string.Empty;

        var activeTraits = profile.GetActiveTraits(3).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("\nPERSONALITY EXPRESSION (use these naturally in your response):");

        foreach (var (name, intensity) in activeTraits)
        {
            if (profile.Traits.TryGetValue(name, out var trait) && trait.ExpressionPatterns.Length > 0)
            {
                string pattern = trait.ExpressionPatterns[_random.Next(trait.ExpressionPatterns.Length)];
                sb.AppendLine($"- {name} ({intensity:P0}): {pattern}");
            }
        }

        // Add mood influence
        sb.AppendLine($"\nCURRENT MOOD: {profile.CurrentMood.Name} (energy: {profile.CurrentMood.Energy:P0}, positivity: {profile.CurrentMood.Positivity:P0})");

        // Add proactivity guidance
        double proactivity = activeTraits.Any(t => t.Name == "curious")
            ? 0.8
            : 0.5 * profile.AdaptabilityScore;

        if (proactivity > 0.6)
        {
            sb.AppendLine("\nPROACTIVE BEHAVIOR: You're curious right now! Ask a follow-up question about something that genuinely interests you about this topic.");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Uses MeTTa reasoning to determine which traits to express based on context.
    /// </summary>
    public async Task<(string[] ActiveTraits, double ProactivityLevel, string? SuggestedQuestion)>
        ReasonAboutResponseAsync(
            string personaName,
            string userInput,
            string conversationContext,
            CancellationToken ct = default)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return (Array.Empty<string>(), 0.5, null);

        // Query MeTTa for trait activation based on context
        var activeTraits = await InferActiveTraitsAsync(profile, userInput, ct);

        // Determine if we should ask a proactive question
        var (shouldAsk, question) = await DetermineProactiveQuestionAsync(profile, userInput, conversationContext, ct);

        // Calculate proactivity level based on profile and context
        double proactivity = CalculateProactivity(profile, userInput);

        return (activeTraits, proactivity, shouldAsk ? question : null);
    }

    private string? GenerateNewCuriosity(PersonalityProfile profile, string topic)
    {
        var questions = new[]
        {
            $"What aspects of {topic} are you most interested in exploring?",
            $"What's driving your interest in {topic} right now?",
            $"Are there specific challenges with {topic} I can help with?",
            $"How does {topic} fit into what you're working on?",
            $"What would make {topic} really click for you?"
        };

        return questions[_random.Next(questions.Length)];
    }
}
