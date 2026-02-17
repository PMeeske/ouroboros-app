namespace Ouroboros.Application;

/// <summary>
/// Exception thrown when a tracked step fails, preserving the updated state.
/// </summary>
public class TrackedStepException : Exception
{
    public CliPipelineState State { get; }

    public TrackedStepException(string message, Exception inner, CliPipelineState state)
        : base(message, inner)
    {
        State = state;
    }
}