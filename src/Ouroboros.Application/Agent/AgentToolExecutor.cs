// <copyright file="AgentToolExecutor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Agent;

/// <summary>
/// Dispatches tool invocations by name against the registered tool dictionary.
/// Provides a safe execution wrapper that catches exceptions and returns error strings.
/// </summary>
public static class AgentToolExecutor
{
    /// <summary>
    /// Executes the named tool with the given arguments.
    /// Returns the tool's string output, or an error message when the tool is
    /// unknown or throws an exception.
    /// </summary>
    public static async Task<string> ExecuteAsync(
        Dictionary<string, Func<string, CliPipelineState, Task<string>>> tools,
        string toolName,
        string args,
        CliPipelineState state)
    {
        if (!tools.TryGetValue(toolName, out var tool))
        {
            return $"Error: Unknown tool '{toolName}'. Available tools: {string.Join(", ", tools.Keys)}";
        }

        try
        {
            return await tool(args, state);
        }
        catch (Exception ex)
        {
            return $"Error executing {toolName}: {ex.Message}";
        }
    }
}
