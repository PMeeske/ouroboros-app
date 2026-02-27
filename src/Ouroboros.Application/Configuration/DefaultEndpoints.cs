namespace Ouroboros.Application.Configuration;

/// <summary>
/// Centralised default endpoint constants used across the application.
/// Replace hardcoded <c>"http://localhost:..."</c> strings with these constants
/// so that a single change propagates everywhere.
/// </summary>
public static class DefaultEndpoints
{
    /// <summary>Ollama local inference server.</summary>
    public const string Ollama = "http://localhost:11434";

    /// <summary>Qdrant gRPC endpoint.</summary>
    public const string QdrantGrpc = "http://localhost:6334";

    /// <summary>Qdrant REST / HTTP endpoint.</summary>
    public const string QdrantRest = "http://localhost:6333";

    /// <summary>Ouroboros API host.</summary>
    public const string OuroborosApi = "http://localhost:5000";

    /// <summary>OpenClaw Gateway WebSocket endpoint.</summary>
    public const string OpenClawGateway = "ws://127.0.0.1:18789";

    /// <summary>Stable Diffusion (Forge / A1111) endpoint.</summary>
    public const string StableDiffusion = "http://localhost:7860";

    /// <summary>MeTTa reasoning service endpoint.</summary>
    public const string MeTTa = "http://localhost:8000";
}
