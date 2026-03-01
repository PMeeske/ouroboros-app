namespace Ouroboros.Android.Services;

/// <summary>
/// Centralised default endpoint constants for the Android client.
/// Mirrors <c>Ouroboros.Application.Configuration.DefaultEndpoints</c> without
/// requiring a project reference to Ouroboros.Application.
/// </summary>
internal static class DefaultEndpoints
{
    /// <summary>Ollama local inference server.</summary>
    public const string Ollama = "http://localhost:11434";

    /// <summary>Ouroboros API host.</summary>
    public const string OuroborosApi = "http://localhost:5000";
}
