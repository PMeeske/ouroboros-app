using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Production implementation of <see cref="IRoomModeService"/>.
/// Adapts <see cref="RoomConfig"/> to the <see cref="RoomMode.RunAsync"/> parameter overload
/// and delegates execution to RoomMode.
/// </summary>
public sealed class RoomModeService : IRoomModeService
{
    private readonly ILogger<RoomModeService> _logger;

    public RoomModeService(ILogger<RoomModeService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RunAsync(RoomConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting room mode — persona={Persona}, model={Model}, endpoint={Endpoint}",
            config.Persona, config.Model, config.Endpoint);

        var room = new RoomMode();

        await room.RunAsync(
            personaName: config.Persona,
            model: config.Model,
            endpoint: config.Endpoint,
            embedModel: config.EmbedModel,
            qdrant: config.QdrantEndpoint,
            azureSpeechKey: config.AzureSpeechKey,
            azureSpeechRegion: config.AzureSpeechRegion,
            ttsVoice: config.TtsVoice,
            localTts: config.LocalTts,
            avatarOn: config.Avatar,
            avatarPort: config.AvatarPort,
            quiet: config.Quiet,
            cooldown: TimeSpan.FromSeconds(config.CooldownSeconds),
            maxPerWindow: config.MaxInterjections,
            phiThreshold: config.PhiThreshold,
            ct: cancellationToken);

        _logger.LogInformation("Room mode session completed");
    }
}
