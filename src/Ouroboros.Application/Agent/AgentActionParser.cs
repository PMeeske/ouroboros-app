// <copyright file="AgentActionParser.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Ouroboros.Application.Agent;

/// <summary>
/// Parses raw LLM text responses into structured <see cref="AgentAction"/> instances.
/// Handles JSON extraction, tool-use payloads, completion signals, and thought fall-through.
/// </summary>
public static class AgentActionParser
{
    /// <summary>
    /// Parses the agent's LLM response into an <see cref="AgentAction"/>.
    /// Extracts the first JSON object from the response and interprets it as
    /// a completion signal, tool invocation, or thought.
    /// </summary>
    public static AgentAction Parse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd <= jsonStart)
            {
                return new AgentAction { Type = AgentActionType.Think, Thought = response };
            }

            var json = response[jsonStart..(jsonEnd + 1)];
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check for completion
            if (root.TryGetProperty("complete", out var complete) && complete.GetBoolean())
            {
                return new AgentAction
                {
                    Type = AgentActionType.Complete,
                    Summary = root.TryGetProperty("summary", out var summary) ? summary.GetString() : "Task completed",
                };
            }

            // Check for tool use
            if (root.TryGetProperty("tool", out var tool))
            {
                var toolName = tool.GetString();
                string? toolArgs = null;

                if (root.TryGetProperty("args", out var args))
                {
                    toolArgs = args.ValueKind == JsonValueKind.String
                        ? args.GetString()
                        : args.GetRawText();
                }

                return new AgentAction
                {
                    Type = AgentActionType.UseTool,
                    ToolName = toolName,
                    ToolArgs = toolArgs,
                };
            }

            // Check for think
            if (root.TryGetProperty("thought", out var thought))
            {
                return new AgentAction { Type = AgentActionType.Think, Thought = thought.GetString() };
            }

            return new AgentAction { Type = AgentActionType.Unknown };
        }
        catch
        {
            // If JSON parsing fails, treat as a thought
            return new AgentAction { Type = AgentActionType.Think, Thought = response };
        }
    }
}
