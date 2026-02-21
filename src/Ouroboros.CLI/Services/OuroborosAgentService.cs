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
            "Starting Ouroboros agent (all modes) — persona={Persona}, model={Model}, endpoint={Endpoint}, voice={Voice}",
            config.Persona, config.Model, config.Endpoint, config.Voice);

        // 1. Load & apply IConfiguration (appsettings, secrets, env vars)
        var configuration = AgentBootstrapper.LoadConfiguration();
        AgentBootstrapper.ApplyConfiguration(configuration);

        // 2. Build ImmersiveMode options from OuroborosConfig
        var immersiveOpts = new ImmersiveCommandVoiceOptions
        {
            Persona        = config.Persona ?? "Iaret",
            Model          = config.Model   ?? "llama3:latest",
            Endpoint       = config.Endpoint ?? "http://localhost:11434",
            EmbedModel     = config.EmbedModel ?? "nomic-embed-text",
            QdrantEndpoint = config.QdrantEndpoint ?? "http://localhost:6334",
            Voice          = config.Voice,
            VoiceOnly      = config.VoiceOnly,
            LocalTts       = config.LocalTts,
            Avatar         = true,
            AvatarPort     = 9471,
            RoomMode       = false,
        };

        // 3. Start RoomMode as a background ambient presence (linked CTS so it
        //    terminates automatically when ImmersiveMode or the outer CT completes)
        using var roomCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var roomTask = Task.Run(async () =>
        {
            try
            {
                await Ouroboros.CLI.Commands.RoomMode.RunRoomAsync(
                    personaName : immersiveOpts.Persona,
                    model        : immersiveOpts.Model,
                    endpoint     : immersiveOpts.Endpoint,
                    embedModel   : immersiveOpts.EmbedModel,
                    qdrant       : immersiveOpts.QdrantEndpoint,
                    azureSpeechKey    : config.AzureSpeechKey,
                    azureSpeechRegion : config.AzureSpeechRegion ?? "eastus",
                    ttsVoice          : config.TtsVoice ?? "en-US-AvaMultilingualNeural",
                    localTts          : config.LocalTts,
                    avatarOn          : true,
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

        // 4. Run ImmersiveMode as primary foreground loop
        _logger.LogInformation("Launching ImmersiveMode (foreground) + RoomMode (background)");
        try
        {
            await Ouroboros.CLI.Commands.ImmersiveMode.RunImmersiveAsync(immersiveOpts, cancellationToken);
        }
        finally
        {
            // Cancel and await room task when immersive exits
            await roomCts.CancelAsync();
            try { await roomTask.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch { /* best-effort */ }
            _logger.LogInformation("Agent session completed");
        }
    }
}
