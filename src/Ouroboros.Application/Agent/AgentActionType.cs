namespace Ouroboros.Application.Agent;

/// <summary>
/// Classification of actions the agent can take during its reasoning loop.
/// </summary>
public enum AgentActionType
{
    /// <summary>Action could not be parsed.</summary>
    Unknown,

    /// <summary>Agent is recording an internal thought.</summary>
    Think,

    /// <summary>Agent wants to invoke a tool.</summary>
    UseTool,

    /// <summary>Agent considers the task finished.</summary>
    Complete,
}