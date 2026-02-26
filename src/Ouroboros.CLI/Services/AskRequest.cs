namespace Ouroboros.CLI.Services;

/// <summary>
/// Typed parameter object for the ask service.
/// Replaces the legacy <c>AskOptions</c> CommandLineParser class for DI-based invocations,
/// enabling all CLI options to flow properly through the handler → service chain.
/// </summary>
public sealed record AskRequest(
    string Question,
    bool UseRag             = false,
    string ModelName        = "llama3:latest",
    string? Endpoint        = null,
    string? ApiKey          = null,
    string? EndpointType    = null,
    double Temperature      = 0.7,
    int MaxTokens           = 2048,
    int TimeoutSeconds      = 60,
    bool Stream             = false,
    string? Culture         = null,
    bool AgentMode          = false,
    string AgentModeType    = "lc",
    int AgentMaxSteps       = 6,
    bool StrictModel        = false,
    string Router           = "direct",
    string? CoderModel      = null,
    string? SummarizeModel  = null,
    string? ReasonModel     = null,
    string? GeneralModel    = null,
    string EmbedModel       = "nomic-embed-text",
    int TopK                = 3,
    bool Debug              = false,
    bool JsonTools          = false,
    string Persona          = "Iaret",
    bool VoiceOnly          = false,
    bool LocalTts           = false,
    bool VoiceLoop          = false);
