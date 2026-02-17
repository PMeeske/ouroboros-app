namespace Ouroboros.Application.Personality;

/// <summary>
/// Categorizes thoughts by their origin and purpose.
/// </summary>
public enum ThoughtOrigin
{
    /// <summary>Triggered by external input (user message).</summary>
    Reactive,
    /// <summary>Arises spontaneously from internal state.</summary>
    Autonomous,
    /// <summary>Generated as part of a thinking chain.</summary>
    Chained,
    /// <summary>Recalled from memory or past sessions.</summary>
    Recalled,
    /// <summary>Synthesized from multiple sources.</summary>
    Synthesized
}