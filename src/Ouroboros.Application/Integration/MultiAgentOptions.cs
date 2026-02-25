namespace Ouroboros.Application.Integration;

/// <summary>Options for multi-agent coordination configuration.</summary>
public sealed record MultiAgentOptions(
    int MaxAgents = 10,
    string CoordinationStrategy = "Hierarchical",
    bool EnableCommunication = true);