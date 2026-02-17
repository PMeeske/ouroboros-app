using Ouroboros.Core.Ethics;

namespace Ouroboros.Application.GitHub;

/// <summary>
/// Result of a self-modification workflow execution.
/// </summary>
public sealed record SelfModificationResult(
    bool Success,
    PullRequestInfo? PullRequest,
    string? BranchName,
    string? Error,
    EthicalClearance EthicsClearance);