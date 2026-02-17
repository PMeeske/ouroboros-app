namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Blueprint describing a neuron to be assembled.
/// </summary>
public sealed record NeuronBlueprint
{
    /// <summary>Unique name for the neuron.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Why this neuron is needed.</summary>
    public required string Rationale { get; init; }

    /// <summary>Type of neuron.</summary>
    public NeuronType Type { get; init; } = NeuronType.Custom;

    /// <summary>Topics this neuron subscribes to.</summary>
    public required IReadOnlyList<string> SubscribedTopics { get; init; }

    /// <summary>Capabilities required.</summary>
    public IReadOnlyList<NeuronCapability> Capabilities { get; init; } = [];

    /// <summary>Message handlers.</summary>
    public IReadOnlyList<MessageHandler> MessageHandlers { get; init; } = [];

    /// <summary>Whether this neuron has autonomous tick behavior.</summary>
    public bool HasAutonomousTick { get; init; }

    /// <summary>Description of autonomous tick behavior.</summary>
    public string? TickBehaviorDescription { get; init; }

    /// <summary>Confidence score (0-1).</summary>
    public double ConfidenceScore { get; init; }

    /// <summary>Identifier of what generated this blueprint.</summary>
    public string? IdentifiedBy { get; init; }
}