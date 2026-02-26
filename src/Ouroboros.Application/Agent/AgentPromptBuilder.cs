// <copyright file="AgentPromptBuilder.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;

namespace Ouroboros.Application.Agent;

/// <summary>
/// Builds the system/instruction prompts sent to the LLM during each agent iteration.
/// Assembles tool descriptions, conversation history, and action summaries into a
/// single prompt string.
/// </summary>
public static class AgentPromptBuilder
{
    /// <summary>
    /// Returns a description block for all agent tools, auto-generated from
    /// <see cref="AgentToolFactory.ToolDescriptors"/>. This keeps the prompt
    /// in sync with the actual tool implementations.
    /// </summary>
    public static string BuildToolDescriptions()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Available Tools");
        sb.AppendLine();
        sb.AppendLine("Use tools by responding with JSON in this format:");
        sb.AppendLine("""{"tool": "tool_name", "args": {"arg1": "value1", ...}}""");
        sb.AppendLine();

        foreach (var tool in AgentToolFactory.ToolDescriptors)
        {
            sb.AppendLine($"### {tool.Name}");
            sb.AppendLine(tool.Description);
            sb.AppendLine($"Args: {tool.ArgsExample}");
            sb.AppendLine();
        }

        sb.AppendLine("## Completing the Task");
        sb.AppendLine();
        sb.AppendLine("When the task is complete, respond with:");
        sb.AppendLine("""{"complete": true, "summary": "Description of what was accomplished"}""");

        return sb.ToString();
    }

    /// <summary>
    /// Assembles the full agent prompt for one iteration of the reasoning loop.
    /// Includes the system instructions, tool descriptions, current task,
    /// executed actions so far, and recent conversation history.
    /// </summary>
    public static string Build(
        string task,
        string toolDescriptions,
        List<AgentMessage> history,
        List<string> executedActions)
    {
        var sb = new StringBuilder();

        sb.AppendLine("""
            You are an autonomous AI agent, similar to GitHub Copilot. You can read files, write code,
            run commands, and complete programming tasks independently.

            ## Your Behavior
            1. ALWAYS respond with valid JSON (a tool call or completion)
            2. Think step-by-step about what needs to be done
            3. Use tools to gather information before making changes
            4. When editing files, include enough context to uniquely identify the location
            5. Verify your changes by reading files or running tests
            6. IMPORTANT: Once you have gathered enough information, mark the task as complete!

            ## Important Rules
            - Never guess file contents - always read first
            - Make small, incremental changes
            - If you encounter an error, analyze it and try a different approach
            - If stuck, ask the user for clarification
            - DO NOT read the same file multiple times - analyze what you already have
            - When you have the answer, complete immediately with a detailed summary
            """);

        sb.AppendLine(toolDescriptions);

        sb.AppendLine($"\n## Current Task\n{task}");

        if (executedActions.Count > 0)
        {
            sb.AppendLine("\n## Actions Taken So Far");
            foreach (var action in executedActions.TakeLast(10))
            {
                sb.AppendLine($"- {action}");
            }
        }

        if (history.Count > 0)
        {
            sb.AppendLine("\n## Conversation History");
            foreach (var msg in history.TakeLast(10))
            {
                // Tool results need more context, assistant messages can be shorter
                var maxLen = msg.Role == "tool" ? 2000 : 500;
                sb.AppendLine($"[{msg.Role}]: {StringHelpers.TruncateForDisplay(msg.Content, maxLen)}");
            }
        }

        sb.AppendLine("\n## Your Next Action (respond with JSON only):");

        return sb.ToString();
    }
}
