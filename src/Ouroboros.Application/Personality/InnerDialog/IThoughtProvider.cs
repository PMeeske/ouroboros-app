namespace Ouroboros.Application.Personality;

/// <summary>
/// Interface for pluggable thought generation.
/// Implement this to add custom thought types or processing logic.
/// </summary>
public interface IThoughtProvider
{
    /// <summary>Unique name for this provider.</summary>
    string Name { get; }

    /// <summary>Priority order (lower = runs first).</summary>
    int Order { get; }

    /// <summary>Whether this provider can generate thoughts in the given context.</summary>
    bool CanProcess(ThoughtContext context);

    /// <summary>Generates thoughts based on the context.</summary>
    Task<ThoughtProviderResult> GenerateThoughtsAsync(ThoughtContext context, CancellationToken ct = default);
}