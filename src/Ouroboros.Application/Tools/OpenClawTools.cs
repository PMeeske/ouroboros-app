// <copyright file="OpenClawTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// OpenClaw Gateway tools — gives the agent a communication nervous system
/// through WhatsApp, Telegram, Slack, Discord, Signal, iMessage, and device nodes.
/// Individual tool implementations live in the <c>Tools/OpenClaw/</c> directory.
/// </summary>
public static class OpenClawTools
{
    /// <summary>Shared gateway client, set during init.</summary>
    public static OpenClawGatewayClient? SharedClient
    {
        get => OpenClawSharedState.SharedClient;
        set => OpenClawSharedState.SharedClient = value;
    }

    /// <summary>Shared security policy for message/node validation.</summary>
    public static OpenClawSecurityPolicy? SharedPolicy
    {
        get => OpenClawSharedState.SharedPolicy;
        set => OpenClawSharedState.SharedPolicy = value;
    }

    /// <summary>
    /// Connects to the OpenClaw Gateway, sets up security, and populates
    /// <see cref="SharedClient"/> / <see cref="SharedPolicy"/>.
    /// CLI option values take precedence; env vars are used as fallback.
    /// </summary>
    /// <param name="gatewayUrl">Explicit gateway URL (from --openclaw-gateway), or null for env/default.</param>
    /// <param name="token">Explicit token (from --openclaw-token), or null for env.</param>
    /// <returns>The resolved gateway URL on success, or null if connection failed.</returns>
    public static Task<string?> ConnectGatewayAsync(
        string? gatewayUrl = null,
        string? token = null)
        => OpenClawSharedState.ConnectGatewayAsync(gatewayUrl, token);

    /// <summary>Gets all OpenClaw tools.</summary>
    public static IEnumerable<ITool> GetAllTools()
    {
        // Core operator tools
        yield return new OpenClawStatusTool();
        yield return new OpenClawListChannelsTool();
        yield return new OpenClawNodeListTool();
        yield return new OpenClawSendMessageTool();
        yield return new OpenClawNodeInvokeTool();

        // Session management
        yield return new OpenClawSessionsListTool();
        yield return new OpenClawSessionsHistoryTool();
        yield return new OpenClawSessionsSendTool();
        yield return new OpenClawSessionsSpawnTool();

        // Memory
        yield return new OpenClawMemorySearchTool();
        yield return new OpenClawMemoryGetTool();

        // Hardware / node shortcuts
        yield return new OpenClawCameraSnapTool();
        yield return new OpenClawCameraClipTool();
        yield return new OpenClawLocationGetTool();
        yield return new OpenClawScreenRecordNodeTool();

        // Scheduling
        yield return new OpenClawCronListTool();
        yield return new OpenClawCronAddTool();
        yield return new OpenClawCronToggleTool();
        yield return new OpenClawCronRunsTool();

        // Gateway management
        yield return new OpenClawHealthTool();
        yield return new OpenClawDevicesListTool();
        yield return new OpenClawDevicesApproveTool();
        yield return new OpenClawDevicesRevokeTool();

        // Incoming messages
        yield return new OpenClawGetMessagesTool();
        yield return new OpenClawPollMessagesTool();
    }

    /// <summary>Adds OpenClaw tools to a registry.</summary>
    public static ToolRegistry WithOpenClawTools(this ToolRegistry registry)
    {
        foreach (var tool in GetAllTools())
            registry = registry.WithTool(tool);
        return registry;
    }
}
