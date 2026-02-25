namespace Ouroboros.Application.Integration;

/// <summary>Options for embodied agent configuration.</summary>
public sealed record EmbodiedAgentOptions(
    string EnvironmentType = "Simulated",
    int SensorDimensions = 64,
    int ActuatorDimensions = 32);