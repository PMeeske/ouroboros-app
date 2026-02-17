namespace Ouroboros.Options;

/// <summary>
/// Interface for commands that support voice mode.
/// </summary>
public interface IVoiceOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether voice mode is enabled.
    /// </summary>
    bool Voice { get; set; }

    /// <summary>
    /// Gets or sets the persona name for voice mode.
    /// </summary>
    string Persona { get; set; }

    /// <summary>
    /// Gets or sets the LLM model for voice mode.
    /// </summary>
    string Model { get; set; }

    /// <summary>
    /// Gets or sets the LLM endpoint URL.
    /// </summary>
    string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the embedding model for semantic search.
    /// </summary>
    string EmbedModel { get; set; }

    /// <summary>
    /// Gets or sets the Qdrant endpoint for skill storage.
    /// </summary>
    string QdrantEndpoint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether voice-only mode is enabled.
    /// </summary>
    bool VoiceOnly { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to prefer local TTS.
    /// </summary>
    bool LocalTts { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to continue voice conversation after command.
    /// </summary>
    bool VoiceLoop { get; set; }
}