using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Setup;
using Ouroboros.Options;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Production implementation of <see cref="IOuroborosAgentService"/>.
/// By default launches all modes: ImmersiveMode as the primary foreground
/// experience and RoomMode as an ambient background presence, both wired
/// to the same model/endpoint from config.
/// </summary>
public class OuroborosAgentService : IOuroborosAgentService
{
    private readonly ILogger<OuroborosAgentService> _logger;
    private readonly IServiceProvider _hostServices;

    public OuroborosAgentService(
        ILogger<OuroborosAgentService> logger,
        IServiceProvider hostServices)
    {
        _logger = logger;
        _hostServices = hostServices;
    }

    /// <inheritdoc />
    public async Task RunAgentAsync(OuroborosConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting Ouroboros agent — persona={Persona}, model={Model}, endpoint={Endpoint}",
            config.Persona, config.Model, config.Endpoint);

        // 1. Load & apply IConfiguration (appsettings, secrets, env vars)
        var configuration = AgentBootstrapper.LoadConfiguration();
        AgentBootstrapper.ApplyConfiguration(configuration);

        // 2. Create OuroborosAgent as single source of truth (all subsystems owned here)
        var agent = Microsoft.Extensions.DependencyInjection.ActivatorUtilities
            .CreateInstance<OuroborosAgent>(_hostServices, config);

        try
        {
            await agent.InitializeAsync();

            // 3. Wire ImmersiveMode — agent owns all subsystems + Iaret persona + avatar
            ImmersiveMode.ConfigureSubsystems(agent.SubModels, agent.SubTools, agent.SubMemory, agent.SubAutonomy);
            ImmersiveMode.ConfigurePersona(agent.IaretPersona);
            ImmersiveMode.ConfigureAvatarService(agent.AvatarService);  // enables speaking/mood face animations

            // 4. Wire RoomMode — same agent subsystems for model + memory sharing
            RoomMode.ConfigureSubsystems(agent.SubModels, agent.SubMemory, agent.SubAutonomy);

            // 5. Build ImmersiveMode options (voice/TTS passthrough — models come from agent)
            var immersiveOpts = new ImmersiveCommandVoiceOptions
            {
                Persona           = config.Persona ?? "Iaret",
                Model             = config.Model   ?? "llama3:latest",
                Endpoint          = config.Endpoint ?? "http://localhost:11434",
                EmbedModel        = config.EmbedModel ?? "nomic-embed-text",
                QdrantEndpoint    = config.QdrantEndpoint ?? "http://localhost:6334",
                Voice             = config.Voice,
                VoiceOnly         = config.VoiceOnly,
                LocalTts          = config.LocalTts,
                AzureTts          = config.AzureTts,
                TtsVoice          = config.TtsVoice ?? "en-US-AvaMultilingualNeural",
                AzureSpeechKey    = config.AzureSpeechKey,
                AzureSpeechRegion = config.AzureSpeechRegion ?? "eastus",
                Avatar            = true,
                AvatarPort        = 9471,
                RoomMode          = false,
            };

            // 6. Start RoomMode as background ambient presence (linked CTS)
            using var roomCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var roomTask = Task.Run(async () =>
            {
                try
                {
                    await RoomMode.RunRoomAsync(
                        personaName       : immersiveOpts.Persona,
                        model             : immersiveOpts.Model,
                        endpoint          : immersiveOpts.Endpoint,
                        embedModel        : immersiveOpts.EmbedModel,
                        qdrant            : immersiveOpts.QdrantEndpoint,
                        azureSpeechKey    : config.AzureSpeechKey,
                        azureSpeechRegion : config.AzureSpeechRegion ?? "eastus",
                        ttsVoice          : config.TtsVoice ?? "en-US-AvaMultilingualNeural",
                        localTts          : config.LocalTts,
                        avatarOn          : false,  // OuroborosAgent's EmbodimentSubsystem already owns the avatar
                        avatarPort        : 9471,
                        quiet             : true,   // silent banner — ImmersiveMode owns the console
                        ct                : roomCts.Token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RoomMode background task exited: {Message}", ex.Message);
                }
            }, roomCts.Token);

            // 7. Run ImmersiveMode as primary foreground loop (Iaret's interactive face)
            _logger.LogInformation("Launching ImmersiveMode (foreground) + RoomMode (background)");
            try
            {
                await ImmersiveMode.RunImmersiveAsync(immersiveOpts, cancellationToken);
            }
            finally
            {
                await roomCts.CancelAsync();
                try { await roomTask.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch { /* best-effort */ }
            }
        }
        finally
        {
            if (agent is IAsyncDisposable d) await d.DisposeAsync();
            _logger.LogInformation("Agent session completed");
        }
    }
}
