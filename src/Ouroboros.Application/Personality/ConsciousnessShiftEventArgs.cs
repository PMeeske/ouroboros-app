namespace Ouroboros.Application.Personality;

/// <summary>Event args for consciousness shifts.</summary>
public class ConsciousnessShiftEventArgs : EventArgs
{
    public string NewEmotion { get; }
    public double ArousalChange { get; }
    public ConsciousnessState NewState { get; }

    public ConsciousnessShiftEventArgs(string emotion, double arousalChange, ConsciousnessState state)
    {
        NewEmotion = emotion;
        ArousalChange = arousalChange;
        NewState = state;
    }
}