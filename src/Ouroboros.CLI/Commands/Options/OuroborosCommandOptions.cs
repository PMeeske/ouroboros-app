using System.CommandLine;
using System.CommandLine.Parsing;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Full options for the ouroboros agent command using System.CommandLine 2.0.3 GA.
/// Maps 1:1 to legacy OuroborosOptions (CommandLineParser) for full parity.
/// Every option here corresponds to a property on <see cref="OuroborosConfig"/>.
/// </summary>
public class OuroborosCommandOptions
{
    // ═══════════════════════════════════════════════════════════════════════════
    // VOICE & INTERACTION
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<bool> VoiceOption { get; } = new("--voice", "-v")
    {
        Description = "Enable voice mode (speak & listen)",
        DefaultValueFactory = _ => true
    };

    public System.CommandLine.Option<bool> TextOnlyOption { get; } = new("--text-only")
    {
        Description = "Disable voice, use text input/output only",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> VoiceOnlyOption { get; } = new("--voice-only")
    {
        Description = "Voice-only mode (no text output)",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> LocalTtsOption { get; } = new("--local-tts")
    {
        Description = "Prefer local TTS (Windows SAPI) over Azure",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> AzureTtsOption { get; } = new("--azure-tts")
    {
        Description = "Use Azure Text-to-Speech (default when available)",
        DefaultValueFactory = _ => true
    };

    public System.CommandLine.Option<string?> AzureSpeechKeyOption { get; } = new("--azure-speech-key")
    {
        Description = "Azure Speech API key (or set AZURE_SPEECH_KEY env var)"
    };

    public System.CommandLine.Option<string> AzureSpeechRegionOption { get; } = new("--azure-speech-region")
    {
        Description = "Azure Speech region",
        DefaultValueFactory = _ => "eastus"
    };

    public System.CommandLine.Option<string> TtsVoiceOption { get; } = new("--tts-voice")
    {
        Description = "Azure TTS voice name",
        DefaultValueFactory = _ => "en-US-AvaMultilingualNeural"
    };

    public System.CommandLine.Option<bool> VoiceChannelOption { get; } = new("--voice-channel")
    {
        Description = "Enable parallel voice side channel for persona-specific audio",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> VoiceV2Option { get; } = new("--voice-v2")
    {
        Description = "Enable unified Rx streaming voice mode V2",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> ListenOption { get; } = new("--listen")
    {
        Description = "Enable voice input (speech-to-text) on startup",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<string?> WakeWordOption { get; } = new("--wake-word")
    {
        Description = "Wake word phrase to activate listening (default: 'Hey Iaret')",
        DefaultValueFactory = _ => "Hey Iaret"
    };

    public System.CommandLine.Option<bool> NoWakeWordOption { get; } = new("--no-wake-word")
    {
        Description = "Always-on listening without wake word",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<string> SttBackendOption { get; } = new("--stt-backend")
    {
        Description = "STT backend: azure, whisper, or auto (default: auto = Azure if key present, else Whisper)",
        DefaultValueFactory = _ => "auto"
    };

    public System.CommandLine.Option<bool> VoiceLoopOption { get; } = new("--voice-loop")
    {
        Description = "Continue voice conversation in loop",
        DefaultValueFactory = _ => true
    };

    public System.CommandLine.Option<string> PersonaOption { get; } = new("--persona")
    {
        Description = "Persona: Iaret, Aria, Echo, Sage, Atlas",
        DefaultValueFactory = _ => "Iaret"
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // LLM & MODEL CONFIGURATION
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<string> ModelOption { get; } = new("--model", "-m")
    {
        Description = "LLM model name",
        DefaultValueFactory = _ => "deepseek-v3.1:671b-cloud"
    };

    public System.CommandLine.Option<string?> CultureOption { get; } = new("--culture", "-c")
    {
        Description = "Target culture for the response (e.g. en-US, fr-FR, es)"
    };

    public System.CommandLine.Option<string> EndpointOption { get; } = new("--endpoint")
    {
        Description = "LLM endpoint URL",
        DefaultValueFactory = _ => "http://localhost:11434"
    };

    public System.CommandLine.Option<string?> ApiKeyOption { get; } = new("--api-key")
    {
        Description = "API key for remote endpoint"
    };

    public System.CommandLine.Option<string?> EndpointTypeOption { get; } = new("--endpoint-type")
    {
        Description = "Provider type: auto|anthropic|openai|azure|google|mistral|deepseek|groq|together|fireworks|perplexity|cohere|ollama|github-models|litellm|huggingface|replicate"
    };

    public System.CommandLine.Option<double> TemperatureOption { get; } = new("--temperature")
    {
        Description = "Sampling temperature",
        DefaultValueFactory = _ => 0.7
    };

    public System.CommandLine.Option<int> MaxTokensOption { get; } = new("--max-tokens")
    {
        Description = "Max tokens for completion",
        DefaultValueFactory = _ => 2048
    };

    public System.CommandLine.Option<int> TimeoutSecondsOption { get; } = new("--timeout")
    {
        Description = "Request timeout in seconds",
        DefaultValueFactory = _ => 120
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // EMBEDDINGS & MEMORY
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<string> EmbedModelOption { get; } = new("--embed-model")
    {
        Description = "Embedding model name",
        DefaultValueFactory = _ => "nomic-embed-text"
    };

    public System.CommandLine.Option<string> EmbedEndpointOption { get; } = new("--embed-endpoint")
    {
        Description = "Embedding endpoint (defaults to local Ollama)",
        DefaultValueFactory = _ => "http://localhost:11434"
    };

    public System.CommandLine.Option<string> QdrantEndpointOption { get; } = new("--qdrant")
    {
        Description = "Qdrant endpoint for persistent memory",
        DefaultValueFactory = _ => "http://localhost:6334"
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // FEATURE TOGGLES (All enabled by default for max experience)
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<bool> NoSkillsOption { get; } = new("--no-skills")
    {
        Description = "Disable skill learning subsystem",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> NoMeTTaOption { get; } = new("--no-metta")
    {
        Description = "Disable MeTTa symbolic reasoning",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> NoToolsOption { get; } = new("--no-tools")
    {
        Description = "Disable dynamic tools (web search, etc.)",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> NoPersonalityOption { get; } = new("--no-personality")
    {
        Description = "Disable personality engine & affect",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> NoMindOption { get; } = new("--no-mind")
    {
        Description = "Disable autonomous mind (inner thoughts)",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> NoBrowserOption { get; } = new("--no-browser")
    {
        Description = "Disable Playwright browser automation",
        DefaultValueFactory = _ => false
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // AUTONOMOUS/PUSH MODE
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<bool> PushOption { get; } = new("--push")
    {
        Description = "Enable push mode - Ouroboros proposes actions for your approval",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> PushVoiceOption { get; } = new("--push-voice")
    {
        Description = "Enable voice in push mode",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> YoloOption { get; } = new("--yolo")
    {
        Description = "YOLO mode - full autonomous operation, auto-approve ALL actions (use with caution!)",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<string> AutoApproveOption { get; } = new("--auto-approve")
    {
        Description = "Auto-approve intention categories: safe,memory,analysis (comma-separated)",
        DefaultValueFactory = _ => ""
    };

    public System.CommandLine.Option<int> IntentionIntervalOption { get; } = new("--intention-interval")
    {
        Description = "Seconds between autonomous intention proposals",
        DefaultValueFactory = _ => 45
    };

    public System.CommandLine.Option<int> DiscoveryIntervalOption { get; } = new("--discovery-interval")
    {
        Description = "Seconds between autonomous topic discovery",
        DefaultValueFactory = _ => 90
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // GOVERNANCE & SELF-MODIFICATION
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<bool> EnableSelfModOption { get; } = new("--enable-self-mod")
    {
        Description = "Enable self-modification for agent autonomy",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<string> RiskLevelOption { get; } = new("--risk-level")
    {
        Description = "Minimum risk level for approval: Low|Medium|High|Critical",
        DefaultValueFactory = _ => "Medium"
    };

    public System.CommandLine.Option<bool> AutoApproveLowOption { get; } = new("--auto-approve-low")
    {
        Description = "Auto-approve low-risk modifications",
        DefaultValueFactory = _ => true
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // INITIAL TASK (Optional)
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<string?> GoalOption { get; } = new("--goal", "-g")
    {
        Description = "Initial goal to accomplish (starts planning immediately)"
    };

    public System.CommandLine.Option<string?> QuestionOption { get; } = new("--question", "-q")
    {
        Description = "Initial question to answer"
    };

    public System.CommandLine.Option<string?> DslOption { get; } = new("--dsl", "-d")
    {
        Description = "Pipeline DSL to execute immediately"
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // MULTI-MODEL ORCHESTRATION
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<string?> CoderModelOption { get; } = new("--coder-model")
    {
        Description = "Model for code/refactor tasks"
    };

    public System.CommandLine.Option<string?> ReasonModelOption { get; } = new("--reason-model")
    {
        Description = "Model for strategic reasoning"
    };

    public System.CommandLine.Option<string?> SummarizeModelOption { get; } = new("--summarize-model")
    {
        Description = "Model for summarization"
    };

    public System.CommandLine.Option<string?> VisionModelOption { get; } = new("--vision-model")
    {
        Description = "Model for visual understanding (e.g. qwen3-vl:235b-cloud)"
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // AGENT BEHAVIOR
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<int> AgentMaxStepsOption { get; } = new("--agent-max-steps")
    {
        Description = "Max steps for agent planning",
        DefaultValueFactory = _ => 10
    };

    public System.CommandLine.Option<int> ThinkingIntervalOption { get; } = new("--thinking-interval")
    {
        Description = "Seconds between autonomous thoughts",
        DefaultValueFactory = _ => 30
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // PIPING & BATCH MODE
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<bool> PipeOption { get; } = new("--pipe")
    {
        Description = "Enable pipe mode - read commands from stdin, output to stdout",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<string?> BatchFileOption { get; } = new("--batch")
    {
        Description = "Batch file containing commands to execute (one per line)"
    };

    public System.CommandLine.Option<bool> JsonOutputOption { get; } = new("--json-output")
    {
        Description = "Output responses as JSON for scripting",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> NoGreetingOption { get; } = new("--no-greeting")
    {
        Description = "Skip greeting in non-interactive mode",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> ExitOnErrorOption { get; } = new("--exit-on-error")
    {
        Description = "Exit immediately on command error in batch/pipe mode",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<string?> ExecOption { get; } = new("--exec", "-e")
    {
        Description = "Execute a single command and exit (supports | piping syntax)"
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // DEBUG & OUTPUT
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<bool> DebugOption { get; } = new("--debug")
    {
        Description = "Enable debug logging",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> TraceOption { get; } = new("--trace")
    {
        Description = "Enable trace output for pipelines",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> MetricsOption { get; } = new("--metrics")
    {
        Description = "Show performance metrics",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> StreamOption { get; } = new("--stream")
    {
        Description = "Stream responses as generated",
        DefaultValueFactory = _ => true
    };

    public System.CommandLine.Option<bool> VerboseOption { get; } = new("--verbose")
    {
        Description = "Show full initialization output and debug messages",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> QuietOption { get; } = new("--quiet")
    {
        Description = "Suppress all system messages, show responses only",
        DefaultValueFactory = _ => false
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // COST TRACKING & EFFICIENCY
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<bool> ShowCostsOption { get; } = new("--show-costs")
    {
        Description = "Display token counts and API costs after each response",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> CostAwareOption { get; } = new("--cost-aware")
    {
        Description = "Inject cost-awareness guidelines into system prompt",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> CostSummaryOption { get; } = new("--cost-summary")
    {
        Description = "Show session cost summary on exit",
        DefaultValueFactory = _ => true
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // COLLECTIVE MIND (Multi-Provider)
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<bool> CollectiveModeOption { get; } = new("--collective")
    {
        Description = "Enable collective mind mode (uses multiple LLM providers)",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<string?> CollectivePresetOption { get; } = new("--collective-preset")
    {
        Description = "Collective mind preset: single|local|balanced|fast|premium|budget|anthropic-ollama|anthropic-ollama-lite"
    };

    public System.CommandLine.Option<string> CollectiveThinkingModeOption { get; } = new("--collective-mode")
    {
        Description = "Collective thinking mode: racing|sequential|ensemble|adaptive",
        DefaultValueFactory = _ => "adaptive"
    };

    public System.CommandLine.Option<string?> CollectiveProvidersOption { get; } = new("--collective-providers")
    {
        Description = "Comma-separated list of providers (e.g., anthropic,openai,deepseek,groq,ollama)"
    };

    public System.CommandLine.Option<bool> FailoverOption { get; } = new("--failover")
    {
        Description = "Enable automatic failover to other providers on error",
        DefaultValueFactory = _ => true
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // ELECTION & ORCHESTRATION
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<string> ElectionStrategyOption { get; } = new("--election")
    {
        Description = "Election strategy: majority|weighted|borda|condorcet|runoff|approval|master",
        DefaultValueFactory = _ => "weighted"
    };

    public System.CommandLine.Option<string?> MasterModelOption { get; } = new("--master")
    {
        Description = "Designate master model for orchestration"
    };

    public System.CommandLine.Option<string> EvalCriteriaOption { get; } = new("--eval-criteria")
    {
        Description = "Evaluation criteria preset: default|quality|speed|cost",
        DefaultValueFactory = _ => "default"
    };

    public System.CommandLine.Option<bool> ShowElectionOption { get; } = new("--show-election")
    {
        Description = "Show election results and voting details",
        DefaultValueFactory = _ => false
    };

    public System.CommandLine.Option<bool> ShowOptimizationOption { get; } = new("--show-optimization")
    {
        Description = "Show model optimization suggestions after session",
        DefaultValueFactory = _ => false
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // INTERACTIVE AVATAR
    // ═══════════════════════════════════════════════════════════════════════════

    public System.CommandLine.Option<bool> AvatarOption { get; } = new("--avatar")
    {
        Description = "Launch interactive avatar viewer (auto-opens browser)",
        DefaultValueFactory = _ => true
    };

    public System.CommandLine.Option<int> AvatarPortOption { get; } = new("--avatar-port")
    {
        Description = "Override avatar viewer port (default: auto-assign from 9471)",
        DefaultValueFactory = _ => 0
    };

    public System.CommandLine.Option<string?> SdEndpointOption { get; } = new("--sd-endpoint")
    {
        Description = "Stable Diffusion (Forge/A1111) endpoint for avatar video stream (default: http://localhost:7860)"
    };

    public System.CommandLine.Option<string?> SdModelOption { get; } = new("--sd-model")
    {
        Description = "Stable Diffusion model name for avatar video stream (default: stable-diffusion)"
    };

    // ── Room Presence ─────────────────────────────────────────────────────

    public System.CommandLine.Option<bool> RoomModeOption { get; } = new("--room-mode")
    {
        Description = "Run ambient room-presence listener alongside the normal interactive session",
        DefaultValueFactory = _ => false
    };

    /// <summary>
    /// Adds all ouroboros command options to the given command.
    /// </summary>
    public void AddToCommand(Command command)
    {
        // Voice & Interaction
        command.Add(VoiceOption);
        command.Add(TextOnlyOption);
        command.Add(VoiceOnlyOption);
        command.Add(LocalTtsOption);
        command.Add(AzureTtsOption);
        command.Add(AzureSpeechKeyOption);
        command.Add(AzureSpeechRegionOption);
        command.Add(TtsVoiceOption);
        command.Add(VoiceChannelOption);
        command.Add(VoiceV2Option);
        command.Add(ListenOption);
        command.Add(WakeWordOption);
        command.Add(NoWakeWordOption);
        command.Add(SttBackendOption);
        command.Add(VoiceLoopOption);
        command.Add(PersonaOption);

        // LLM & Model
        command.Add(ModelOption);
        command.Add(CultureOption);
        command.Add(EndpointOption);
        command.Add(ApiKeyOption);
        command.Add(EndpointTypeOption);
        command.Add(TemperatureOption);
        command.Add(MaxTokensOption);
        command.Add(TimeoutSecondsOption);

        // Embeddings & Memory
        command.Add(EmbedModelOption);
        command.Add(EmbedEndpointOption);
        command.Add(QdrantEndpointOption);

        // Feature Toggles
        command.Add(NoSkillsOption);
        command.Add(NoMeTTaOption);
        command.Add(NoToolsOption);
        command.Add(NoPersonalityOption);
        command.Add(NoMindOption);
        command.Add(NoBrowserOption);

        // Autonomous/Push Mode
        command.Add(PushOption);
        command.Add(PushVoiceOption);
        command.Add(YoloOption);
        command.Add(AutoApproveOption);
        command.Add(IntentionIntervalOption);
        command.Add(DiscoveryIntervalOption);

        // Governance & Self-Modification
        command.Add(EnableSelfModOption);
        command.Add(RiskLevelOption);
        command.Add(AutoApproveLowOption);

        // Initial Task
        command.Add(GoalOption);
        command.Add(QuestionOption);
        command.Add(DslOption);

        // Multi-Model Orchestration
        command.Add(CoderModelOption);
        command.Add(ReasonModelOption);
        command.Add(SummarizeModelOption);
        command.Add(VisionModelOption);

        // Agent Behavior
        command.Add(AgentMaxStepsOption);
        command.Add(ThinkingIntervalOption);

        // Piping & Batch Mode
        command.Add(PipeOption);
        command.Add(BatchFileOption);
        command.Add(JsonOutputOption);
        command.Add(NoGreetingOption);
        command.Add(ExitOnErrorOption);
        command.Add(ExecOption);

        // Debug & Output
        command.Add(DebugOption);
        command.Add(TraceOption);
        command.Add(MetricsOption);
        command.Add(StreamOption);
        command.Add(VerboseOption);
        command.Add(QuietOption);

        // Cost Tracking
        command.Add(ShowCostsOption);
        command.Add(CostAwareOption);
        command.Add(CostSummaryOption);

        // Collective Mind
        command.Add(CollectiveModeOption);
        command.Add(CollectivePresetOption);
        command.Add(CollectiveThinkingModeOption);
        command.Add(CollectiveProvidersOption);
        command.Add(FailoverOption);

        // Election & Orchestration
        command.Add(ElectionStrategyOption);
        command.Add(MasterModelOption);
        command.Add(EvalCriteriaOption);
        command.Add(ShowElectionOption);
        command.Add(ShowOptimizationOption);

        // Interactive Avatar
        command.Add(AvatarOption);
        command.Add(AvatarPortOption);
        command.Add(SdEndpointOption);
        command.Add(SdModelOption);

        // Room Presence
        command.Add(RoomModeOption);
    }

    /// <summary>
    /// Binds a <see cref="ParseResult"/> to a fully populated <see cref="OuroborosConfig"/>.
    /// This replaces the 150+ lines of manual <c>parseResult.GetValue(...)</c> calls
    /// that were previously inline in Program.cs.
    /// </summary>
    /// <param name="parseResult">The parse result from System.CommandLine.</param>
    /// <param name="globalVoiceOption">The global --voice option shared across commands.</param>
    /// <returns>A fully populated <see cref="OuroborosConfig"/>.</returns>
    public OuroborosConfig BindConfig(ParseResult parseResult, Option<bool>? globalVoiceOption = null)
    {
        // Voice & Interaction
        var voice         = parseResult.GetValue(VoiceOption);
        var textOnly      = parseResult.GetValue(TextOnlyOption);
        var voiceOnly     = parseResult.GetValue(VoiceOnlyOption);
        var localTts      = parseResult.GetValue(LocalTtsOption);
        var azureTts      = parseResult.GetValue(AzureTtsOption);
        var azureSpeechKey = parseResult.GetValue(AzureSpeechKeyOption);
        var azureSpeechRegion = parseResult.GetValue(AzureSpeechRegionOption) ?? "eastus";
        var ttsVoice      = parseResult.GetValue(TtsVoiceOption) ?? "en-US-AvaMultilingualNeural";
        var voiceChannel  = parseResult.GetValue(VoiceChannelOption);
        var voiceV2       = parseResult.GetValue(VoiceV2Option);
        var listen        = parseResult.GetValue(ListenOption);
        var noWakeWord    = parseResult.GetValue(NoWakeWordOption);
        var wakeWord      = noWakeWord ? null : parseResult.GetValue(WakeWordOption);
        var sttBackend    = parseResult.GetValue(SttBackendOption) ?? "auto";
        var persona       = parseResult.GetValue(PersonaOption) ?? "Iaret";

        // LLM & Model
        var model         = parseResult.GetValue(ModelOption) ?? "llama3:latest";
        var culture       = parseResult.GetValue(CultureOption);
        var endpoint      = parseResult.GetValue(EndpointOption) ?? "http://localhost:11434";
        var apiKey        = parseResult.GetValue(ApiKeyOption);
        var endpointType  = parseResult.GetValue(EndpointTypeOption);
        var temperature   = parseResult.GetValue(TemperatureOption);
        var maxTokens     = parseResult.GetValue(MaxTokensOption);
        var timeoutSec    = parseResult.GetValue(TimeoutSecondsOption);

        // Embeddings & Memory
        var embedModel    = parseResult.GetValue(EmbedModelOption) ?? "nomic-embed-text";
        var embedEndpoint = parseResult.GetValue(EmbedEndpointOption) ?? "http://localhost:11434";
        var qdrantEndpoint = parseResult.GetValue(QdrantEndpointOption) ?? "http://localhost:6334";

        // Feature Toggles
        var noSkills      = parseResult.GetValue(NoSkillsOption);
        var noMetta       = parseResult.GetValue(NoMeTTaOption);
        var noTools       = parseResult.GetValue(NoToolsOption);
        var noPersonality = parseResult.GetValue(NoPersonalityOption);
        var noMind        = parseResult.GetValue(NoMindOption);
        var noBrowser     = parseResult.GetValue(NoBrowserOption);

        // Autonomous/Push Mode
        var push          = parseResult.GetValue(PushOption);
        var pushVoice     = parseResult.GetValue(PushVoiceOption);
        var yolo          = parseResult.GetValue(YoloOption);
        var autoApprove   = parseResult.GetValue(AutoApproveOption) ?? "";
        var intentionInterval  = parseResult.GetValue(IntentionIntervalOption);
        var discoveryInterval  = parseResult.GetValue(DiscoveryIntervalOption);

        // Governance & Self-Modification
        var enableSelfMod = parseResult.GetValue(EnableSelfModOption);
        var riskLevel     = parseResult.GetValue(RiskLevelOption) ?? "Medium";
        var autoApproveLow = parseResult.GetValue(AutoApproveLowOption);

        // Initial Task
        var goal          = parseResult.GetValue(GoalOption);
        var question      = parseResult.GetValue(QuestionOption);
        var dsl           = parseResult.GetValue(DslOption);

        // Multi-Model
        var coderModel    = parseResult.GetValue(CoderModelOption);
        var reasonModel   = parseResult.GetValue(ReasonModelOption);
        var summarizeModel = parseResult.GetValue(SummarizeModelOption);
        var visionModel   = parseResult.GetValue(VisionModelOption);

        // Agent Behavior
        var agentMaxSteps = parseResult.GetValue(AgentMaxStepsOption);
        var thinkingInterval = parseResult.GetValue(ThinkingIntervalOption);

        // Piping & Batch
        var pipe          = parseResult.GetValue(PipeOption);
        var batchFile     = parseResult.GetValue(BatchFileOption);
        var jsonOutput    = parseResult.GetValue(JsonOutputOption);
        var noGreeting    = parseResult.GetValue(NoGreetingOption);
        var exitOnError   = parseResult.GetValue(ExitOnErrorOption);
        var exec          = parseResult.GetValue(ExecOption);

        // Debug & Output
        var debug         = parseResult.GetValue(DebugOption);
        var stream        = parseResult.GetValue(StreamOption);
        var verbose       = parseResult.GetValue(VerboseOption);
        var quiet         = parseResult.GetValue(QuietOption);

        var verbosity = quiet ? OutputVerbosity.Quiet
            : (debug || verbose) ? OutputVerbosity.Verbose
            : OutputVerbosity.Normal;

        // Cost Tracking
        var showCosts     = parseResult.GetValue(ShowCostsOption);
        var costAware     = parseResult.GetValue(CostAwareOption);
        var costSummary   = parseResult.GetValue(CostSummaryOption);

        // Collective Mind
        var collectiveMode = parseResult.GetValue(CollectiveModeOption);
        var collectivePreset = parseResult.GetValue(CollectivePresetOption);
        var collectiveThinkingMode = parseResult.GetValue(CollectiveThinkingModeOption) ?? "adaptive";
        var collectiveProviders = parseResult.GetValue(CollectiveProvidersOption);
        var failover      = parseResult.GetValue(FailoverOption);

        // Election & Orchestration
        var electionStrategy = parseResult.GetValue(ElectionStrategyOption) ?? "weighted";
        var masterModel   = parseResult.GetValue(MasterModelOption);
        var evalCriteria  = parseResult.GetValue(EvalCriteriaOption) ?? "default";
        var showElection  = parseResult.GetValue(ShowElectionOption);
        var showOptimization = parseResult.GetValue(ShowOptimizationOption);

        // Interactive Avatar
        var avatar        = parseResult.GetValue(AvatarOption);
        var avatarPort    = parseResult.GetValue(AvatarPortOption);
        var sdEndpoint    = parseResult.GetValue(SdEndpointOption);
        var sdModel       = parseResult.GetValue(SdModelOption);

        // Room Presence
        var roomMode      = parseResult.GetValue(RoomModeOption);

        // Derive Azure TTS
        var azureKey = azureSpeechKey ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var useAzureTts = localTts ? false : (azureTts && !string.IsNullOrEmpty(azureKey));

        return new OuroborosConfig(
            Persona: persona,
            Model: model,
            Endpoint: endpoint,
            EmbedModel: embedModel,
            EmbedEndpoint: embedEndpoint,
            QdrantEndpoint: qdrantEndpoint,
            ApiKey: apiKey ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
            EndpointType: endpointType,
            Voice: (push || yolo) ? pushVoice : (voice && !textOnly),
            VoiceOnly: voiceOnly,
            LocalTts: localTts,
            AzureTts: useAzureTts,
            AzureSpeechKey: azureKey,
            AzureSpeechRegion: azureSpeechRegion,
            TtsVoice: ttsVoice,
            VoiceChannel: voiceChannel,
            VoiceV2: voiceV2,
            Listen: listen,
            WakeWord: wakeWord,
            SttBackend: sttBackend,
            Debug: debug,
            Verbosity: verbosity,
            Temperature: temperature,
            MaxTokens: maxTokens,
            Culture: culture,
            EnableSkills: !noSkills,
            EnableMeTTa: !noMetta,
            EnableTools: !noTools,
            EnablePersonality: !noPersonality,
            EnableMind: !noMind,
            EnableBrowser: !noBrowser,
            EnablePush: push,
            YoloMode: yolo,
            AutoApproveCategories: autoApprove,
            IntentionIntervalSeconds: intentionInterval,
            DiscoveryIntervalSeconds: discoveryInterval,
            EnableSelfModification: enableSelfMod,
            RiskLevel: riskLevel,
            AutoApproveLow: autoApproveLow,
            ThinkingIntervalSeconds: thinkingInterval,
            AgentMaxSteps: agentMaxSteps,
            InitialGoal: goal,
            InitialQuestion: question,
            InitialDsl: dsl,
            CoderModel: coderModel,
            ReasonModel: reasonModel,
            SummarizeModel: summarizeModel,
            VisionModel: visionModel,
            PipeMode: pipe,
            BatchFile: batchFile,
            JsonOutput: jsonOutput,
            NoGreeting: noGreeting || pipe || !string.IsNullOrWhiteSpace(batchFile) || !string.IsNullOrWhiteSpace(exec),
            ExitOnError: exitOnError,
            ExecCommand: exec,
            ShowCosts: showCosts,
            CostAware: costAware,
            CostSummary: costSummary,
            CollectiveMode: collectiveMode,
            CollectivePreset: collectivePreset,
            CollectiveThinkingMode: collectiveThinkingMode,
            CollectiveProviders: collectiveProviders,
            Failover: failover,
            ElectionStrategy: electionStrategy,
            MasterModel: masterModel,
            EvaluationCriteria: evalCriteria,
            ShowElection: showElection,
            ShowOptimization: showOptimization,
            Avatar: avatar,
            AvatarPort: avatarPort,
            SdEndpoint: sdEndpoint,
            SdModel: sdModel,
            RoomMode: roomMode
        );
    }
}
