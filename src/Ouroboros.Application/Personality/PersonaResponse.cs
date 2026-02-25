namespace Ouroboros.Application.Personality;

/// <summary>Response from the persona including consciousness metadata.</summary>
public record PersonaResponse
{
    public required string Text { get; init; }
    public required string EmotionalTone { get; init; }
    public required List<string> InnerThoughts { get; init; }
    public required string CognitiveApproach { get; init; }
    public required ConsciousnessState ConsciousnessState { get; init; }
    public double Confidence { get; init; }
}