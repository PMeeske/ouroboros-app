namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for the unified voice mode service.
/// </summary>
public sealed record VoiceModeConfigV2(
    string Persona = "Iaret",
    bool VoiceOnly = false,
    bool EnableTts = true,
    bool EnableStt = true,
    bool EnableVisualIndicators = true,
    string? Culture = null,
    TimeSpan BargeInDebounce = default,
    TimeSpan IdleTimeout = default)
{
    /// <summary>Gets the barge-in debounce with default.</summary>
    public TimeSpan ActualBargeInDebounce => BargeInDebounce == default
        ? TimeSpan.FromMilliseconds(200)
        : BargeInDebounce;

    /// <summary>Gets the idle timeout with default.</summary>
    public TimeSpan ActualIdleTimeout => IdleTimeout == default
        ? TimeSpan.FromMinutes(5)
        : IdleTimeout;
}