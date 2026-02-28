namespace Ouroboros.Easy;

/// <summary>
/// Centralised default endpoint constants for the Easy pipeline library.
/// Mirrors <c>Ouroboros.Application.Configuration.DefaultEndpoints</c> without
/// requiring a project reference to Ouroboros.Application.
/// </summary>
internal static class DefaultEndpoints
{
    /// <summary>Ollama local inference server.</summary>
    public const string Ollama = "http://localhost:11434";
}
