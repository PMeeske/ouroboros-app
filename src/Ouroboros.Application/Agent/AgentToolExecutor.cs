// <copyright file="AgentToolExecutor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Agent;

/// <summary>
/// Dispatches tool invocations by name against the registered tool dictionary.
/// Provides a safe execution wrapper that catches exceptions and returns error strings.
/// Includes a permission gate for dangerous (write/execute) tool invocations.
/// </summary>
public static class AgentToolExecutor
{
    /// <summary>
    /// Tool names that perform write or execute operations and require permission
    /// before execution when <see cref="RequireConfirmation"/> is enabled.
    /// </summary>
    public static readonly HashSet<string> DangerousToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "run_command",
        "write_file",
        "edit_file",
    };

    /// <summary>
    /// When <c>true</c> (the default), dangerous tool invocations will check
    /// <see cref="OnPermissionRequired"/> before executing. If the callback denies
    /// permission, the tool call is blocked with an error message.
    /// </summary>
    public static bool RequireConfirmation { get; set; } = true;

    /// <summary>
    /// Optional callback invoked before executing a dangerous tool.
    /// Parameters: (toolName, toolArgs). Returns <c>true</c> to allow, <c>false</c> to deny.
    /// When <c>null</c> and <see cref="RequireConfirmation"/> is <c>true</c>, dangerous
    /// tool calls are blocked by default (fail-closed).
    /// </summary>
    public static Func<string, string, Task<bool>>? OnPermissionRequired { get; set; }

    /// <summary>
    /// Executes the named tool with the given arguments.
    /// Returns the tool's string output, or an error message when the tool is
    /// unknown, permission is denied, or the tool throws an exception.
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

        // Permission gate for dangerous tools
        if (RequireConfirmation && DangerousToolNames.Contains(toolName))
        {
            if (OnPermissionRequired != null)
            {
                bool allowed;
                try
                {
                    allowed = await OnPermissionRequired(toolName, args);
                }
                catch (Exception ex)
                {
                    return $"Error: Permission check failed for '{toolName}': {ex.Message}";
                }

                if (!allowed)
                    return $"Error: Permission denied for tool '{toolName}'. User rejected the operation.";
            }
            else
            {
                // Fail-closed: no callback registered but confirmation required
                return $"Error: Tool '{toolName}' requires permission but no permission handler is registered.";
            }
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
