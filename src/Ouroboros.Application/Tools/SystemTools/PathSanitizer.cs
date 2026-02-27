// <copyright file="PathSanitizer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

/// <summary>
/// Sanitizes and validates file paths to prevent directory traversal and
/// access to sensitive system locations.
/// </summary>
public static class PathSanitizer
{
    private static readonly HashSet<string> AllowedBaseDirectories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a directory that tools are allowed to access.
    /// When at least one directory is registered, all path access is restricted
    /// to registered directories only (jail mode).
    /// </summary>
    public static void AddAllowedDirectory(string path) => AllowedBaseDirectories.Add(Path.GetFullPath(path));

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

        // Block obvious dangerous paths
        var dangerous = new[] { @"\Windows\System32", @"\etc\shadow", @"\etc\passwd", ".ssh", "credentials" };
        if (dangerous.Any(d => fullPath.Contains(d, StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException($"Access denied: {path}");

        // If allowed dirs configured, enforce jail
        if (AllowedBaseDirectories.Count > 0 && !AllowedBaseDirectories.Any(b => fullPath.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException($"Path outside allowed directories: {path}");

        return fullPath;
    }
}
