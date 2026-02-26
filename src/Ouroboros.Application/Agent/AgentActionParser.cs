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
    /// Tries multiple JSON extraction strategies to handle LLM responses that
    /// embed JSON within surrounding prose or contain multiple JSON objects.
    /// </summary>
    public static AgentAction Parse(string response)
    {
        // Strategy 1: Try extracting balanced JSON objects and interpreting them
        var candidates = ExtractBalancedJsonCandidates(response);

        // Try candidates from last to first — the last JSON object is usually the action
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            var action = TryParseAction(candidates[i]);
            if (action != null && action.Type != AgentActionType.Unknown)
                return action;
        }

        // Strategy 2: Fall back to first-to-last brace span (legacy behavior)
        var jsonStart = response.IndexOf('{');
        var jsonEnd = response.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var action = TryParseAction(response[jsonStart..(jsonEnd + 1)]);
            if (action != null && action.Type != AgentActionType.Unknown)
                return action;
        }

        // No valid JSON action found — if there's text, treat as thought; otherwise unknown
        if (!string.IsNullOrWhiteSpace(response))
            return new AgentAction { Type = AgentActionType.Think, Thought = response };

        return new AgentAction { Type = AgentActionType.Unknown };
    }

    /// <summary>
    /// Extracts all balanced-brace JSON candidates from the response string.
    /// Tracks brace depth to find proper object boundaries.
    /// </summary>
    private static List<string> ExtractBalancedJsonCandidates(string text)
    {
        var candidates = new List<string>();
        int i = 0;

        while (i < text.Length)
        {
            if (text[i] == '{')
            {
                int start = i;
                int depth = 0;
                bool inString = false;
                bool escape = false;

                for (int j = start; j < text.Length; j++)
                {
                    char c = text[j];

                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\' && inString)
                    {
                        escape = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = !inString;
                        continue;
                    }

                    if (!inString)
                    {
                        if (c == '{') depth++;
                        else if (c == '}')
                        {
                            depth--;
                            if (depth == 0)
                            {
                                candidates.Add(text[start..(j + 1)]);
                                i = j + 1;
                                goto nextCandidate;
                            }
                        }
                    }
                }

                // Unbalanced — skip this opening brace
                i = start + 1;
                nextCandidate:;
            }
            else
            {
                i++;
            }
        }

        return candidates;
    }

    /// <summary>
    /// Attempts to parse a JSON string into an <see cref="AgentAction"/>.
    /// Returns null if parsing fails entirely.
    /// </summary>
    private static AgentAction? TryParseAction(string json)
    {
        try
        {
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
            return null;
        }
    }
}
