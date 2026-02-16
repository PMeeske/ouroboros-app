// <copyright file="AgentTypes.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

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

/// <summary>
/// Configuration parsed from the AutoAgent pipeline token arguments.
/// </summary>
public sealed class AutoAgentConfig
{
    public string? Task { get; set; }
    public int MaxIterations { get; set; } = 15;

    /// <summary>
    /// Parses a semicolon-separated argument string into an <see cref="AutoAgentConfig"/>.
    /// Supports: <c>task text;maxIter=N</c>.
    /// </summary>
    public static AutoAgentConfig Parse(string? args)
    {
        var config = new AutoAgentConfig();

        if (string.IsNullOrWhiteSpace(args)) return config;

        // Remove quotes if present
        if (args.StartsWith("'") && args.EndsWith("'")) args = args[1..^1];
        if (args.StartsWith("\"") && args.EndsWith("\"")) args = args[1..^1];

        foreach (var part in args.Split(';'))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("maxIter=", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(trimmed[8..], out var max))
                    config.MaxIterations = max;
            }
            else if (!trimmed.Contains('='))
            {
                config.Task = trimmed;
            }
        }

        return config;
    }
}
