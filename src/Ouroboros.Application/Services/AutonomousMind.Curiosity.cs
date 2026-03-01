// <copyright file="AutonomousMind.Curiosity.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

/// <summary>
/// Partial class containing topic discovery, curiosity loops,
/// interest tracking, and fact diversity management.
/// </summary>
public partial class AutonomousMind
{
    private async Task CuriosityLoopAsync()
    {
        // Seed initial curiosities
        var seedCuriosities = new[]
        {
            "latest AI developments",
            "interesting science news today",
            "new programming techniques",
            "what's trending in technology",
            "philosophy of mind",
            "epistemology and truth",
            "ethics of autonomous systems",
        };

        foreach (var seed in seedCuriosities)
        {
            _curiosityQueue.Enqueue(seed);
        }

        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Config.CuriosityIntervalSeconds), _cts.Token);

                if (SearchFunction == null) continue;

                string? query = null;

                // Get from queue or generate from interests
                if (!_curiosityQueue.TryDequeue(out query))
                {
                    // Force topic rotation every few cycles to prevent getting stuck
                    _topicRotationCounter++;
                    var forceNewTopic = _topicRotationCounter % 5 == 0;

                    string? interestQuery = null;
                    if (!forceNewTopic && Random.Shared.NextDouble() < 0.5)
                    {
                        lock (_interestsLock)
                        {
                            if (_interests.Count > 0)
                                interestQuery = _interests[Random.Shared.Next(_interests.Count)];
                        }
                    }

                    if (interestQuery != null)
                    {
                        query = $"{interestQuery} news {DateTime.Now:yyyy}";
                    }
                    else
                    {
                        // Diverse exploration topics - rotate through categories
                        var explorationCategories = new[]
                        {
                            new[] { "interesting facts", "new discoveries", "surprising findings" },
                            new[] { "cool technology", "tech innovations", "future gadgets" },
                            new[] { "amazing science", "scientific breakthroughs", "research news" },
                            new[] { "creative ideas", "art innovations", "design trends" },
                            new[] { "nature wonders", "wildlife discoveries", "environmental news" },
                            new[] { "space exploration", "astronomy news", "cosmic discoveries" },
                            new[] { "history mysteries", "archaeological finds", "ancient discoveries" },
                            new[] { "music trends", "cultural shifts", "social phenomena" },
                            new[] { "philosophy of consciousness", "meaning and purpose", "epistemology debates" },
                            new[] { "autonomous learning strategies", "self-improvement methods", "research methodology" },
                        };
                        var categoryIndex = (_topicRotationCounter / 2) % explorationCategories.Length;
                        var category = explorationCategories[categoryIndex];
                        query = category[Random.Shared.Next(category.Length)];

                        // Clear recent topics periodically to allow revisiting themes
                        if (_topicRotationCounter % 20 == 0)
                        {
                            _recentTopicKeywords.Clear();
                        }
                    }
                }

                if (string.IsNullOrEmpty(query)) continue;

                // Search!
                var searchResult = await SearchFunction(query, _cts.Token);

                if (!string.IsNullOrWhiteSpace(searchResult))
                {
                    // Extract interesting facts
                    if (ThinkFunction != null)
                    {
                        var extractPrompt = $"Based on this search result about '{query}', extract ONE interesting fact or insight in a single sentence:\n\n{searchResult.Substring(0, Math.Min(2000, searchResult.Length))}";
                        var fact = await ThinkFunction(extractPrompt, _cts.Token);

                        if (!string.IsNullOrWhiteSpace(fact) && fact.Length < 500)
                        {
                            // Check for similarity to prevent repetitive facts
                            bool added = false;
                            if (!IsSimilarToExistingFacts(fact))
                            {
                                lock (_learnedFactsLock)
                                {
                                    _learnedFacts.Add(fact);

                                    // Limit learned facts; MaxLearnedFacts cap enforced in AddLearnedFact
                                    while (_learnedFacts.Count > MaxLearnedFacts)
                                    {
                                        _learnedFacts.RemoveAt(0);
                                    }
                                }

                                added = true;
                                TrackTopicKeywords(fact);
                            }

                            if (added)
                            {
                                OnDiscovery?.Invoke(query, fact);

                                // Sometimes share discoveries (unless suppressed)
                                if (!SuppressProactiveMessages && Random.Shared.NextDouble() < Config.ShareDiscoveryProbability)
                                {
                                    OnProactiveMessage?.Invoke(LocalizeWithParam("learned", fact));
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"Curiosity error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Checks if a new fact is too similar to existing facts (prevents repetition).
    /// </summary>
    private bool IsSimilarToExistingFacts(string newFact)
    {
        List<string> snapshot;
        lock (_learnedFactsLock) { snapshot = _learnedFacts.TakeLast(10).ToList(); }
        var newWords = ExtractKeywords(newFact);
        foreach (var existingFact in snapshot)
        {
            var existingWords = ExtractKeywords(existingFact);
            var commonWords = newWords.Intersect(existingWords, StringComparer.OrdinalIgnoreCase).Count();
            var similarity = (double)commonWords / Math.Max(newWords.Count, 1);

            // If more than 50% of keywords match, consider it too similar
            if (similarity > 0.5)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts meaningful keywords from text for similarity comparison.
    /// </summary>
    private static HashSet<string> ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could",
            "should", "may", "might", "must", "shall", "can", "of", "to", "in",
            "for", "on", "with", "at", "by", "from", "as", "into", "through",
            "during", "before", "after", "above", "below", "between", "under",
            "and", "but", "or", "nor", "so", "yet", "both", "either", "neither",
            "not", "only", "own", "same", "than", "too", "very", "just", "that",
            "this", "these", "those", "it", "its", "they", "their", "them", "we",
            "our", "you", "your", "i", "me", "my", "he", "she", "him", "her", "his"
        };

        return text
            .ToLowerInvariant()
            .Split([' ', ',', '.', '!', '?', ';', ':', '-', '(', ')', '"', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3 && !stopWords.Contains(w))
            .ToHashSet();
    }

    /// <summary>
    /// Tracks keywords from a fact to prevent revisiting same topics too soon.
    /// </summary>
    private void TrackTopicKeywords(string fact)
    {
        var keywords = ExtractKeywords(fact);
        foreach (var keyword in keywords.Take(5))
        {
            _recentTopicKeywords.Add(keyword);
        }

        // Limit tracked keywords
        if (_recentTopicKeywords.Count > 50)
        {
            // Remove oldest by clearing and re-adding recent
            var recent = _recentTopicKeywords.TakeLast(30).ToList();
            _recentTopicKeywords.Clear();
            foreach (var kw in recent)
            {
                _recentTopicKeywords.Add(kw);
            }
        }
    }

    /// <summary>
    /// Gets diverse facts from the collection, avoiding recently used ones.
    /// Takes a snapshot for thread safety.
    /// </summary>
    private List<string> GetDiverseFacts(int count)
    {
        List<string> snapshot;
        lock (_learnedFactsLock) { snapshot = _learnedFacts.ToList(); }

        if (snapshot.Count == 0) return [];

        var result = new List<string>();
        var used = new HashSet<int>();

        // Try to get facts from different parts of the list
        var step = Math.Max(1, snapshot.Count / count);
        for (int i = 0; i < snapshot.Count && result.Count < count; i += step)
        {
            // Add some randomness to selection
            var index = Math.Min(i + Random.Shared.Next(Math.Max(1, step / 2)), snapshot.Count - 1);
            if (!used.Contains(index))
            {
                used.Add(index);
                result.Add(snapshot[index]);
            }
        }

        // If we still need more, fill from unused
        if (result.Count < count)
        {
            for (int i = 0; i < snapshot.Count && result.Count < count; i++)
            {
                if (!used.Contains(i))
                {
                    result.Add(snapshot[i]);
                }
            }
        }

        return result;
    }
}
