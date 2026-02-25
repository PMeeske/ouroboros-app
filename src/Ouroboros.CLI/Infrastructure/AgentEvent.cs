namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Discriminated union of events published on the <see cref="EventBroker{T}"/>
/// by the agent loop and tool subsystem. UI components subscribe to the broker
/// and react without coupling to internal agent state.
/// </summary>
public abstract record AgentEvent;

/// <summary>A tool call has been issued and is now executing.</summary>
public sealed record ToolStartedEvent(
    string ToolName,
    string? Param,
    DateTime Timestamp) : AgentEvent;

/// <summary>A tool call completed (successfully or with error).</summary>
public sealed record ToolCompletedEvent(
    string ToolName,
    bool Success,
    string? Output,
    TimeSpan Elapsed) : AgentEvent;

/// <summary>The agent is thinking / awaiting an LLM response.</summary>
public sealed record AgentThinkingEvent(string Label) : AgentEvent;

/// <summary>The LLM produced a complete response.</summary>
public sealed record AgentResponseEvent(
    string PersonaName,
    string Text,
    TimeSpan Elapsed) : AgentEvent;
