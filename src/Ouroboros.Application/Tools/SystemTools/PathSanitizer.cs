// <copyright file="PathSanitizer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Ouroboros.Application.Tools.SystemTools;

/// <summary>
/// Sanitizes and validates file paths to prevent directory traversal and
/// access to sensitive system locations.
/// </summary>
public static class PathSanitizer
{
    private static readonly ConcurrentDictionary<string, byte> AllowedBaseDirectories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Static initializer adds the current working directory as a default jail.
    /// </summary>
    static PathSanitizer()
    {
        AllowedBaseDirectories.TryAdd(Path.GetFullPath(Environment.CurrentDirectory), 0);
    }

    /// <summary>
    /// Registers a directory that tools are allowed to access.
    /// When at least one directory is registered, all path access is restricted
    /// to registered directories only (jail mode).
    /// </summary>
    public static void AddAllowedDirectory(string path) => AllowedBaseDirectories.TryAdd(Path.GetFullPath(path), 0);

    /// <summary>
    /// Expands environment variables, canonicalizes the path, and checks it
    /// against a blocklist of dangerous locations and, optionally, an allowlist
    /// of permitted base directories.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the path targets a blocked location or falls outside the allowed directories.
    /// </exception>
    public static string Sanitize(string path)
    {
        // Expand env vars then canonicalize
        var expanded = Environment.ExpandEnvironmentVariables(path);
        var fullPath = Path.GetFullPath(expanded);

        // Block obvious dangerous paths (both Windows and Unix separators)
        var dangerous = new[] { @"\Windows\System32", "/Windows/System32", @"\etc\shadow", "/etc/shadow", @"\etc\passwd", "/etc/passwd", ".ssh", "credentials" };
        if (dangerous.Any(d => fullPath.Contains(d, StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException($"Access denied: {path}");

        // If allowed dirs configured, enforce jail
        if (!AllowedBaseDirectories.IsEmpty && !AllowedBaseDirectories.Keys.Any(b => fullPath.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException($"Path outside allowed directories: {path}");

        return fullPath;
    }
}
