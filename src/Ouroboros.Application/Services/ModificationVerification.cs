namespace Ouroboros.Application.Services;

/// <summary>
/// Result of verifying and executing a code modification.
/// Tracks file existence, hash changes, and verification status.
/// </summary>
public sealed record ModificationVerification
{
    /// <summary>Path to the file being modified.</summary>
    public string? FilePath { get; init; }

    /// <summary>Whether the file existed before modification attempt.</summary>
    public bool FileExisted { get; init; }

    /// <summary>File hash before modification.</summary>
    public string? BeforeHash { get; init; }

    /// <summary>File hash after modification.</summary>
    public string? AfterHash { get; init; }

    /// <summary>Whether the modification was verified (file exists, tool executed).</summary>
    public bool WasVerified { get; init; }

    /// <summary>Whether the file content actually changed (hash differs).</summary>
    public bool WasModified { get; init; }

    /// <summary>Error message if verification/modification failed.</summary>
    public string? Error { get; init; }

    /// <summary>Raw result from the modification tool.</summary>
    public string? ToolResult { get; init; }

    /// <summary>When the modification was attempted.</summary>
    public DateTime AttemptedAt { get; init; }
}