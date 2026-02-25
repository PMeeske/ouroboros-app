namespace Ouroboros.Application.Personality;

/// <summary>
/// The type of stimulus in classical conditioning.
/// </summary>
public enum StimulusType
{
    /// <summary>Unconditioned stimulus - naturally triggers a response.</summary>
    Unconditioned,
    /// <summary>Conditioned stimulus - learned association.</summary>
    Conditioned,
    /// <summary>Neutral stimulus - no current association.</summary>
    Neutral,
    /// <summary>Context stimulus - environmental/situational cue.</summary>
    Context,
    /// <summary>Temporal stimulus - time-based trigger.</summary>
    Temporal,
    /// <summary>Social stimulus - person-related trigger.</summary>
    Social,
    /// <summary>Emotional stimulus - feeling-based trigger.</summary>
    Emotional
}