namespace Ouroboros.Application.Personality;

/// <summary>
/// Result from a thought provider.
/// </summary>
public sealed record ThoughtProviderResult(
    List<InnerThought> Thoughts,
    bool ShouldContinue = true,
    string? NextProviderHint = null);