// <copyright file="AgentPromptBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
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
    /// Returns a static description block for all agent tools, including
    /// the JSON schema the LLM must follow when invoking them.
    /// </summary>
    public static string BuildToolDescriptions()
    {
        return """
            ## Available Tools

            Use tools by responding with JSON in this format:
            {"tool": "tool_name", "args": {"arg1": "value1", ...}}

            ### read_file
            Read the contents of a file.
            Args: {"path": "path/to/file.cs"}

            ### write_file
            Create or overwrite a file with new content.
            Args: {"path": "path/to/file.cs", "content": "file contents here"}

            ### edit_file
            Replace specific text in a file. Include enough context to uniquely identify the location.
            Args: {"path": "path/to/file.cs", "old": "text to replace", "new": "replacement text"}

            ### list_dir
            List contents of a directory.
            Args: {"path": "path/to/directory"}

            ### search_files
            Search for text across files.
            Args: {"query": "search text", "path": ".", "pattern": "*.cs"}

            ### run_command
            Execute a PowerShell command.
            Args: {"command": "dotnet build"}

            ### vector_search
            Search the vector store for similar documents (requires UseQdrant).
            Args: {"query": "semantic search query"}

            ### think
            Record your thoughts/planning (no external action).
            Args: {"thought": "I need to first..."}

            ### ask_user
            Ask the user a clarifying question.
            Args: {"question": "What file should I modify?"}

            ## Completing the Task

            When the task is complete, respond with:
            {"complete": true, "summary": "Description of what was accomplished"}
            """;
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
