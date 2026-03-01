using Ouroboros.Application.Configuration;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for the MeTTa orchestrator command.
/// Immutable record carrying all CLI-parsed values for a MeTTa session.
/// Replaces the legacy <c>Ouroboros.Options.MeTTaOptions</c> class.
/// </summary>
public sealed record MeTTaConfig(
    string Goal = "",
    string? Culture = null,
    string Model = "deepseek-v3.1:671b-cloud",
    double Temperature = 0.7,
    int MaxTokens = 2048,
    int TimeoutSeconds = 60,
    string? Endpoint = null,
    string? ApiKey = null,
    string? EndpointType = null,
    bool Debug = false,
    string Embed = "nomic-embed-text",
    string EmbedModel = "nomic-embed-text",
    string QdrantEndpoint = DefaultEndpoints.QdrantGrpc,
    bool PlanOnly = false,
    bool ShowMetrics = true,
    bool Interactive = false,
    string Persona = "Iaret",
    bool Voice = false,
    bool VoiceOnly = false,
    bool LocalTts = false,
    bool VoiceLoop = false);
