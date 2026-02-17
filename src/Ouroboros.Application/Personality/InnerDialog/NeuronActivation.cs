namespace Ouroboros.Application.Personality;

/// <summary>
/// A single neuron activation in the thinking cascade.
/// Represents a thought that has been processed through the neural (Ollama) layer.
/// </summary>
public sealed record NeuronActivation(
    string ThoughtId,
    string Content,
    string[] ActivatedConcepts,
    double ActivationStrength,
    string? ParentThoughtId,
    DateTime Timestamp)
{
    /// <summary>Creates a seed activation with no parent.</summary>
    public static NeuronActivation CreateSeed(string content, string[] concepts, double strength) =>
        new(
            Guid.NewGuid().ToString("N")[..8],
            content,
            concepts,
            strength,
            null,
            DateTime.UtcNow);

    /// <summary>Creates a child activation linked to a parent.</summary>
    public NeuronActivation CreateChild(string content, string[] concepts, double strength) =>
        new(
            Guid.NewGuid().ToString("N")[..8],
            content,
            concepts,
            strength,
            ThoughtId,
            DateTime.UtcNow);
}