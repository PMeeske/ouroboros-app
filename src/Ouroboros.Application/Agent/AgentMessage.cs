namespace Ouroboros.Application.Agent;

/// <summary>
/// A single message in the agent's conversation history.
/// </summary>
public sealed class AgentMessage
{
    public string Role { get; }
    public string Content { get; }

    public AgentMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }
}