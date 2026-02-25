namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Handler specification for a neuron message.
/// </summary>
public sealed record MessageHandler
{
    /// <summary>Topic pattern to match.</summary>
    public required string TopicPattern { get; init; }

    /// <summary>Description of handling logic.</summary>
    public required string HandlingLogic { get; init; }

    /// <summary>Whether this handler sends a direct response.</summary>
    public bool SendsResponse { get; init; }

    /// <summary>Whether this handler broadcasts results.</summary>
    public bool BroadcastsResult { get; init; }
}