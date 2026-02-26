namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for the unified Ouroboros agent.
/// </summary>
public sealed record OuroborosConfig(
    string Persona = "Iaret",
    string Model = "deepseek-v3.1:671b-cloud",
    string Endpoint = "http://localhost:11434",
    string EmbedModel = "nomic-embed-text",
    string EmbedEndpoint = "http://localhost:11434",
    string QdrantEndpoint = "http://localhost:6334",
    string? ApiKey = null,
    string? EndpointType = null,  // auto|openai|ollama-cloud|litellm|github-models|anthropic
    bool Voice = false,
    bool VoiceOnly = false,
    // Defaults to false (prefer cloud TTS for higher quality).
    // Use --local-tts for offline/low-latency scenarios.
    bool LocalTts = false,
    bool AzureTts = false,
    string? AzureSpeechKey = null,
    string AzureSpeechRegion = "eastus",
    string TtsVoice = "en-US-AvaMultilingualNeural",
    bool VoiceChannel = false,
    bool Listen = false,
    string? WakeWord = "Hey Iaret",      // null = always-on (no wake word)
    string SttBackend = "auto",           // "azure" | "whisper" | "auto"
    bool Debug = false,
    OutputVerbosity Verbosity = OutputVerbosity.Normal,
    double Temperature = 0.7,
    int MaxTokens = 2048,
    string? Culture = null,
    // Feature toggles - all enabled by default
    bool EnableSkills = true,
    bool EnableMeTTa = true,
    bool EnableTools = true,
    bool EnablePersonality = true,
    bool EnableMind = true,
    bool EnableBrowser = true,
    bool EnableConsciousness = true,
    bool EnableEmbodiment = true,  // Multimodal sensor/actuator embodiment
    // Autonomous/Push mode
    bool EnablePush = false,
    bool YoloMode = false,
    string AutoApproveCategories = "",
    int IntentionIntervalSeconds = 45,
    int DiscoveryIntervalSeconds = 90,
    // Governance & Self-Modification
    bool EnableSelfModification = false,
    string RiskLevel = "Medium",
    bool AutoApproveLow = true,
    // Additional config
    int ThinkingIntervalSeconds = 30,
    int AgentMaxSteps = 10,
    string? InitialGoal = null,
    string? InitialQuestion = null,
    string? InitialDsl = null,
    // Multi-model
    string? CoderModel = null,
    string? ReasonModel = null,
    string? SummarizeModel = null,
    string? VisionModel = null,
    string? LanguageModel = null,    // language detection sub-model (default: aya-expanse:8b)
    // Piping & Batch mode
    bool PipeMode = false,
    string? BatchFile = null,
    bool JsonOutput = false,
    bool NoGreeting = false,
    bool ExitOnError = false,
    string? ExecCommand = null,
    // Cost tracking & efficiency
    bool ShowCosts = false,
    bool CostAware = false,
    bool CostSummary = true,
    // Collective Mind (Multi-Provider)
    bool CollectiveMode = false,
    string? CollectivePreset = null,  // balanced|fast|premium|budget|local|single
    string CollectiveThinkingMode = "adaptive",  // racing|sequential|ensemble|adaptive
    string? CollectiveProviders = null,  // comma-separated providers
    bool Failover = true,
    // Election & Orchestration
    string ElectionStrategy = "weighted-majority",  // majority|weighted|borda|condorcet|instant-runoff|approval|master
    string? MasterModel = null,  // Provider name to use as master for orchestration
    string EvaluationCriteria = "default",  // default|quality|speed|cost
    bool ShowElection = false,
    bool ShowOptimization = false,
    // Interactive Avatar
    bool Avatar = true,
    bool AvatarCloud = false,
    int AvatarPort = 9471,
    string? SdEndpoint = null,
    string? SdModel = null,
    // Room Presence
    bool RoomMode = false,
    // OpenClaw Gateway integration
    string? OpenClawGateway = "ws://127.0.0.1:18789",
    string? OpenClawToken = null,          // gateway auth token
    bool EnableOpenClaw = true);
