using System.CommandLine;
using System.CommandLine.Parsing;
using Ouroboros.Application.Configuration;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Full options for the ouroboros agent command using System.CommandLine 2.0.3 GA.
/// Maps 1:1 to legacy OuroborosOptions (CommandLineParser) for full parity.
/// Every option here corresponds to a property on <see cref="OuroborosConfig"/>.
/// </summary>
public partial class OuroborosCommandOptions
{
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
        command.Add(AvatarCloudOption);
        command.Add(AvatarPortOption);
        command.Add(SdEndpointOption);
        command.Add(SdModelOption);

        // Room Presence
        command.Add(RoomModeOption);

        // OpenClaw Gateway
        command.Add(EnableOpenClawOption);
        command.Add(OpenClawGatewayOption);
        command.Add(OpenClawTokenOption);
        command.Add(EnablePcNodeOption);
        command.Add(PcNodeConfigOption);
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
        var listen        = parseResult.GetValue(ListenOption);
        var noWakeWord    = parseResult.GetValue(NoWakeWordOption);
        var wakeWord      = noWakeWord ? null : parseResult.GetValue(WakeWordOption);
        var sttBackend    = parseResult.GetValue(SttBackendOption) ?? "auto";
        var persona       = parseResult.GetValue(PersonaOption) ?? "Iaret";

        // LLM & Model
        var model         = parseResult.GetValue(ModelOption) ?? "deepseek-v3.1:671b-cloud";
        var culture       = parseResult.GetValue(CultureOption);
        var endpoint      = parseResult.GetValue(EndpointOption) ?? DefaultEndpoints.Ollama;
        var apiKey        = parseResult.GetValue(ApiKeyOption);
        var endpointType  = parseResult.GetValue(EndpointTypeOption);
        var temperature   = parseResult.GetValue(TemperatureOption);
        var maxTokens     = parseResult.GetValue(MaxTokensOption);
        _ = parseResult.GetValue(TimeoutSecondsOption);

        // Embeddings & Memory
        var embedModel    = parseResult.GetValue(EmbedModelOption) ?? "nomic-embed-text";
        var embedEndpoint = parseResult.GetValue(EmbedEndpointOption) ?? DefaultEndpoints.Ollama;
        var qdrantEndpoint = parseResult.GetValue(QdrantEndpointOption) ?? DefaultEndpoints.QdrantGrpc;

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
        _ = parseResult.GetValue(StreamOption);
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
        var avatarCloud    = parseResult.GetValue(AvatarCloudOption);
        var avatarPort    = parseResult.GetValue(AvatarPortOption);
        var sdEndpoint    = parseResult.GetValue(SdEndpointOption);
        var sdModel       = parseResult.GetValue(SdModelOption);

        // Room Presence
        var roomMode      = parseResult.GetValue(RoomModeOption);

        // OpenClaw Gateway
        var enableOpenClaw = parseResult.GetValue(EnableOpenClawOption);
        var openClawGateway = parseResult.GetValue(OpenClawGatewayOption);
        var openClawToken  = parseResult.GetValue(OpenClawTokenOption)
                             ?? Environment.GetEnvironmentVariable("OPENCLAW_TOKEN");
        var enablePcNode   = parseResult.GetValue(EnablePcNodeOption);
        var pcNodeConfig   = parseResult.GetValue(PcNodeConfigOption);

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
            AvatarCloud: avatarCloud,
            AvatarPort: avatarPort,
            SdEndpoint: sdEndpoint,
            SdModel: sdModel,
            RoomMode: roomMode,
            OpenClawGateway: enableOpenClaw ? openClawGateway : null,
            OpenClawToken: openClawToken,
            EnableOpenClaw: enableOpenClaw,
            EnablePcNode: enablePcNode,
            PcNodeConfigPath: pcNodeConfig
        );
    }
}
