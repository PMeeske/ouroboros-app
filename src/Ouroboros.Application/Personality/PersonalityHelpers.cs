// <copyright file="PersonalityHelpers.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.Application.Personality;

/// <summary>
/// Static utility methods shared across personality sub-engines.
/// </summary>
public static class PersonalityHelpers
{
    /// <summary>
    /// Sanitizes text for embedding by removing problematic characters and truncating.
    /// </summary>
    public static string SanitizeForEmbedding(string? text, int maxLength = 4000)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // Remove null characters and other control characters that cause encoding issues
        var sanitized = new System.Text.StringBuilder(Math.Min(text.Length, maxLength));
        foreach (char c in text)
        {
            // Skip control characters except newlines/tabs, and skip surrogate pairs if incomplete
            if (c == '\n' || c == '\r' || c == '\t' || (!char.IsControl(c) && !char.IsSurrogate(c)))
            {
                sanitized.Append(c);
            }
            else if (char.IsHighSurrogate(c))
            {
                // Keep valid surrogate pairs (emoji, etc.)
                int idx = text.IndexOf(c);
                if (idx + 1 < text.Length && char.IsLowSurrogate(text[idx + 1]))
                {
                    sanitized.Append(c);
                }
            }
            else if (char.IsLowSurrogate(c))
            {
                // Only append if preceded by high surrogate (handled above)
                if (sanitized.Length > 0 && char.IsHighSurrogate(sanitized[sanitized.Length - 1]))
                {
                    sanitized.Append(c);
                }
            }

            if (sanitized.Length >= maxLength) break;
        }

        return sanitized.ToString();
    }

    /// <summary>
    /// Cleans text for Qdrant payload by ensuring valid UTF-8 encoding.
    /// </summary>
    public static string CleanForPayload(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // Convert to UTF-8 bytes and back to ensure valid encoding
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            // If encoding fails, strip all non-ASCII
            return new string(text.Where(c => c < 128).ToArray());
        }
    }

    /// <summary>
    /// Truncates text to a maximum length, adding ellipsis if needed.
    /// </summary>
    public static string TruncateText(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";

    /// <summary>
    /// Extracts keywords from text, filtering out stop words.
    /// </summary>
    public static string[] ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could", "should", "may", "might",
            "i", "you", "he", "she", "it", "we", "they", "what", "which", "who", "this", "that", "these", "those",
            "and", "or", "but", "if", "then", "else", "when", "where", "how", "why", "all", "each", "every",
            "both", "few", "more", "most", "other", "some", "such", "no", "nor", "not", "only", "own", "same",
            "so", "than", "too", "very", "just", "can", "now", "to", "of", "in", "for", "on", "with", "at", "by" };

        return text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct()
            .Take(10)
            .ToArray();
    }

    /// <summary>
    /// Checks if text contains any of the specified keywords (case-insensitive).
    /// </summary>
    public static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
