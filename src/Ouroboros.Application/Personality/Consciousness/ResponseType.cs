namespace Ouroboros.Application.Personality;

/// <summary>
/// The type of response in conditioning.
/// </summary>
public enum ResponseType
{
    /// <summary>Unconditioned response - natural/innate.</summary>
    Unconditioned,
    /// <summary>Conditioned response - learned through association.</summary>
    Conditioned,
    /// <summary>Anticipatory response - expectation-based.</summary>
    Anticipatory,
    /// <summary>Emotional response - affective reaction.</summary>
    Emotional,
    /// <summary>Behavioral response - action tendency.</summary>
    Behavioral,
    /// <summary>Cognitive response - thought pattern.</summary>
    Cognitive
}