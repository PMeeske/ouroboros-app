namespace Ouroboros.Application.Integration;

/// <summary>Options for consciousness scaffold configuration.</summary>
public sealed record ConsciousnessOptions(
    int MaxWorkspaceSize = 100,
    TimeSpan DefaultItemLifetime = default,
    double MinAttentionThreshold = 0.5)
{
    /// <summary>Gets default consciousness options.</summary>
    public static ConsciousnessOptions Default => new(
        DefaultItemLifetime: TimeSpan.FromMinutes(5));
}