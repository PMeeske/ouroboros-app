// <copyright file="StringHelpers.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Agent;

/// <summary>
/// Shared string utilities used across agent components.
/// </summary>
internal static class StringHelpers
{
    /// <summary>
    /// Truncates a string for display purposes, collapsing newlines to spaces.
    /// </summary>
    public static string TruncateForDisplay(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = text.Replace("\r\n", " ").Replace("\n", " ");
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
