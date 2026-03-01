// <copyright file="AgentTypes.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Agent;

/// <summary>
/// Represents a single action parsed from the agent's LLM response.
/// </summary>
public sealed class AgentAction
{
    public AgentActionType Type { get; set; }
    public string? ToolName { get; set; }
    public string? ToolArgs { get; set; }
    public string? Thought { get; set; }
    public string? Summary { get; set; }
}