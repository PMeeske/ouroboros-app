using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;
using Ouroboros.Options;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Production implementation of <see cref="IImmersiveModeService"/>.
/// Adapts <see cref="ImmersiveConfig"/> to <see cref="ImmersiveCommandVoiceOptions"/>
/// and delegates to <see cref="ImmersiveMode.RunImmersiveAsync"/>.
/// </summary>
public sealed class ImmersiveModeService : IImmersiveModeService
{
    private readonly ILogger<ImmersiveModeService> _logger;

    public ImmersiveModeService(ILogger<ImmersiveModeService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RunAsync(ImmersiveConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting immersive mode — persona={Persona}, model={Model}, endpoint={Endpoint}",
            config.Persona, config.Model, config.Endpoint);

        var opts = new ImmersiveCommandVoiceOptions
        {
            Persona        = config.Persona,
            Model          = config.Model,
            Endpoint       = config.Endpoint,
            EmbedModel     = config.EmbedModel,
            QdrantEndpoint = config.QdrantEndpoint,
            Voice          = config.Voice,
            VoiceOnly      = config.VoiceOnly,
            LocalTts       = config.LocalTts,
            VoiceLoop      = config.VoiceLoop,
            Avatar         = config.Avatar,
            AvatarPort     = config.AvatarPort,
            RoomMode       = config.RoomMode,
            AzureTts       = config.AzureTts,
            TtsVoice       = config.TtsVoice,
            AzureSpeechKey = config.AzureSpeechKey,
            AzureSpeechRegion = config.AzureSpeechRegion,
        };

        await ImmersiveMode.RunImmersiveAsync(opts, cancellationToken);

        _logger.LogInformation("Immersive mode session completed");
    }
}
