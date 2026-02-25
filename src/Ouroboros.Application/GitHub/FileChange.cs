// <copyright file="GitHubTypes.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.GitHub;

/// <summary>
/// Represents a file change for GitHub operations.
/// </summary>
public sealed record FileChange
{
    /// <summary>
    /// Gets the file path relative to repository root.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the new content of the file.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the type of change being made.
    /// </summary>
    public required FileChangeType ChangeType { get; init; }
}