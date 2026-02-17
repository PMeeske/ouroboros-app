namespace Ouroboros.Application.Personality;

/// <summary>Event args for autonomous thoughts.</summary>
public class AutonomousThoughtEventArgs : EventArgs
{
    public InnerThought Thought { get; }
    public AutonomousThoughtEventArgs(InnerThought thought) => Thought = thought;
}