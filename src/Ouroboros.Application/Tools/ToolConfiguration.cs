namespace Ouroboros.Application.Tools;

/// <summary>
/// Configuration for a dynamically optimized tool.
/// Used as the gene type for genetic algorithm evolution.
/// </summary>
public sealed record ToolConfiguration(
    string Name,
    string Description,
    string? SearchProvider,
    double TimeoutSeconds,
    int MaxRetries,
    bool CacheResults,
    string? CustomParameters)
{
    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static ToolConfiguration Default(string name, string description) =>
        new(name, description, null, 30.0, 3, true, null);
}