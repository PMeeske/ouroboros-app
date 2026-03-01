// <copyright file="VectorCliSteps.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using LangChain.Databases;
using Ouroboros.Core.Configuration;

namespace Ouroboros.Application;

/// <summary>
/// CLI Pipeline steps for vector store operations.
/// Supports in-memory, Qdrant, and other IVectorStore implementations.
/// Note: Use semicolon (;) as separator inside quotes since pipe (|) is the DSL step separator.
/// </summary>
public static partial class VectorCliSteps
{
    #region Helper Methods

    internal static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        if (arg.StartsWith("'") && arg.EndsWith("'") && arg.Length >= 2) return arg[1..^1];
        if (arg.StartsWith("\"") && arg.EndsWith("\"") && arg.Length >= 2) return arg[1..^1];
        return arg;
    }

    internal static (string Type, string? ConnectionString, string CollectionName) ParseVectorArgs(string? args)
    {
        string type = "InMemory";
        string? connectionString = null;
        string collectionName = "pipeline_vectors";

        if (string.IsNullOrWhiteSpace(args))
        {
            return (type, connectionString, collectionName);
        }

        string parsed = ParseString(args);

        // Use semicolon as separator since pipe (|) is the DSL step separator
        if (parsed.Contains(';'))
        {
            foreach (var part in parsed.Split(';'))
            {
                if (part.StartsWith("connection=")) connectionString = part[11..];
                else if (part.StartsWith("collection=")) collectionName = part[11..];
                else if (!part.Contains('=')) type = part;
            }
        }
        else
        {
            // Single value - treat as type or connection string
            if (parsed.StartsWith("http://") || parsed.StartsWith("https://"))
            {
                connectionString = parsed;
                type = "Qdrant"; // Assume Qdrant if URL provided
            }
            else
            {
                type = parsed;
            }
        }

        return (type, connectionString, collectionName);
    }

    internal static (string Path, string Pattern) ParseDirArgs(string? args)
    {
        string path = ".";
        string pattern = "*.*";

        if (string.IsNullOrWhiteSpace(args))
        {
            return (path, pattern);
        }

        string parsed = ParseString(args);

        // Use semicolon as separator since pipe (|) is the DSL step separator
        if (parsed.Contains(';'))
        {
            foreach (var part in parsed.Split(';'))
            {
                if (part.StartsWith("pattern=")) pattern = part[8..];
                else if (!part.Contains('=')) path = part;
            }
        }
        else
        {
            path = parsed;
        }

        return (path, pattern);
    }

    internal static List<string> ChunkText(string text, int chunkSize)
    {
        var chunks = new List<string>();

        // Split by paragraphs first
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new System.Text.StringBuilder();

        foreach (var para in paragraphs)
        {
            if (currentChunk.Length + para.Length > chunkSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
            }

            if (para.Length > chunkSize)
            {
                // Split long paragraph by sentences or lines
                var lines = para.Split(new[] { ". ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (currentChunk.Length + line.Length > chunkSize && currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                    }
                    currentChunk.Append(line);
                    currentChunk.Append(' ');
                }
            }
            else
            {
                currentChunk.Append(para);
                currentChunk.Append("\n\n");
            }
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks.Count > 0 ? chunks : new List<string> { text };
    }

    /// <summary>
    /// Sanitize text for embedding by removing problematic characters.
    /// </summary>
    internal static string SanitizeForEmbedding(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            // Keep printable ASCII and common Unicode
            if (c >= 32 && c < 127) // Printable ASCII
            {
                sb.Append(c);
            }
            else if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c))
            {
                sb.Append(c);
            }
            else if (c == '\n' || c == '\r' || c == '\t')
            {
                sb.Append(c);
            }

            // Skip other control characters and problematic Unicode
        }

        return sb.ToString();
    }

    #endregion
}

