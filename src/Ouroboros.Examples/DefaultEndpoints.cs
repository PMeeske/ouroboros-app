namespace Ouroboros.Examples;

/// <summary>
/// Centralised default endpoint constants for examples.
/// Re-exports values from <c>Ouroboros.Application.Configuration.DefaultEndpoints</c>
/// for convenient use in example code.
/// </summary>
internal static class DefaultEndpoints
{
    /// <summary>Qdrant REST / HTTP endpoint.</summary>
    public const string QdrantRest = Ouroboros.Application.Configuration.DefaultEndpoints.QdrantRest;

    /// <summary>MeTTa reasoning service endpoint.</summary>
    public const string MeTTa = Ouroboros.Application.Configuration.DefaultEndpoints.MeTTa;
}
