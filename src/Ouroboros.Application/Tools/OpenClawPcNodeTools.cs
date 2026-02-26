// <copyright file="OpenClawPcNodeTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;
using Ouroboros.Application.OpenClaw.PcNode;

namespace Ouroboros.Application.Tools;

/// <summary>
/// PC node management tools — allows the agent to inspect and control
/// the local PC node's capabilities and pending approval requests.
/// </summary>
public static class OpenClawPcNodeTools
{
    /// <summary>Shared PC node instance, set during init.</summary>
    public static OpenClawPcNode? SharedPcNode { get; set; }

    /// <summary>Shared event bus for incoming event buffering.</summary>
    public static OpenClawEventBus? SharedEventBus { get; set; }

    /// <summary>
    /// Starts the PC node and connects to the gateway.
    /// </summary>
    /// <param name="config">Security configuration for the node.</param>
    /// <param name="gatewayUrl">Gateway WebSocket URL.</param>
    /// <param name="token">Authentication token.</param>
    /// <param name="deviceIdentity">Optional device identity for signing.</param>
    public static async Task<OpenClawPcNode?> StartPcNodeAsync(
        PcNodeSecurityConfig config,
        string? gatewayUrl = null,
        string? token = null,
        OpenClawDeviceIdentity? deviceIdentity = null)
    {
        string resolvedGateway = gatewayUrl
            ?? Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY")
            ?? "ws://127.0.0.1:18789";
        string? resolvedToken = token
            ?? Environment.GetEnvironmentVariable("OPENCLAW_TOKEN");

        var node = new OpenClawPcNode(config, deviceIdentity);

        // Wire event bus
        var eventBus = new OpenClawEventBus();
        node.OnEvent += evt => eventBus.Publish(evt);
        SharedEventBus = eventBus;

        try
        {
            await node.ConnectAsync(new Uri(resolvedGateway), resolvedToken);
            SharedPcNode = node;
            return node;
        }
        catch
        {
            await node.DisposeAsync();
            throw;
        }
    }

    /// <summary>Gets all PC node management tools.</summary>
    public static IEnumerable<ITool> GetAllTools()
    {
        yield return new OpenClawPcCapabilitiesTool();
        yield return new OpenClawPcToggleCapabilityTool();
        yield return new OpenClawApprovalListTool();
        yield return new OpenClawApprovalRespondTool();
    }

    /// <summary>Adds PC node management tools to a registry.</summary>
    public static ToolRegistry WithPcNodeTools(this ToolRegistry registry)
    {
        foreach (var tool in GetAllTools())
            registry = registry.WithTool(tool);
        return registry;
    }

    private static Result<string, string> NodeNotRunning() =>
        Result<string, string>.Failure("PC node is not running. Ensure --enable-pc-node is set.");

    // ═════════════════════════════════════════════════════════════════════════════
    // openclaw_pc_capabilities
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawPcCapabilitiesTool : ITool
    {
        public string Name => "openclaw_pc_capabilities";
        public string Description => "List PC node capabilities with their risk level and enabled/disabled status.";
        public string? JsonSchema => null;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedPcNode == null)
                return Task.FromResult(NodeNotRunning());

            var capabilities = SharedPcNode.GetCapabilityStatus();
            var lines = capabilities.Select(c =>
                $"  {(c.Enabled ? "[ON] " : "[OFF]")} {c.Name,-20} {c.Risk,-10} {c.Description}");
            var summary = $"PC Node Capabilities ({capabilities.Count(c => c.Enabled)}/{capabilities.Count} enabled):\n"
                        + string.Join("\n", lines);

            return Task.FromResult(Result<string, string>.Success(summary));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // openclaw_pc_toggle_capability
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawPcToggleCapabilityTool : ITool
    {
        public string Name => "openclaw_pc_toggle_capability";
        public string Description => "Enable or disable a PC node capability at runtime.";
        public string? JsonSchema => """{"type":"object","properties":{"capability":{"type":"string","description":"Capability name (e.g. 'file.read', 'system.run')"},"enabled":{"type":"boolean","description":"true to enable, false to disable"}},"required":["capability","enabled"]}""";

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedPcNode == null)
                return Task.FromResult(NodeNotRunning());

            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                var capability = root.GetProperty("capability").GetString() ?? "";
                var enabled = root.GetProperty("enabled").GetBoolean();

                // Verify the capability is registered
                var handler = SharedPcNode.Capabilities.GetHandler(capability);
                if (handler == null)
                    return Task.FromResult(Result<string, string>.Failure(
                        $"Unknown capability: '{capability}'. Available: {string.Join(", ", SharedPcNode.Capabilities.Names)}"));

                // Toggle in the security config
                if (enabled)
                    SharedPcNode.Config.EnabledCapabilities.Add(capability);
                else
                    SharedPcNode.Config.EnabledCapabilities.Remove(capability);

                return Task.FromResult(Result<string, string>.Success(
                    $"Capability '{capability}' is now {(enabled ? "enabled" : "disabled")}"));
            }
            catch (JsonException)
            {
                return Task.FromResult(Result<string, string>.Failure(
                    "Invalid JSON. Expected: {\"capability\":\"...\", \"enabled\":true}"));
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // openclaw_approval_list  (pending approvals)
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tracks pending approval requests for the PC node.
    /// </summary>
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ApprovalRequest>
        PendingApprovals = new();

    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource<bool>>
        ApprovalCompletions = new();

    public sealed class OpenClawApprovalListTool : ITool
    {
        public string Name => "openclaw_approval_list";
        public string Description => "List pending PC node approval requests that need a response.";
        public string? JsonSchema => null;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedPcNode == null)
                return Task.FromResult(NodeNotRunning());

            if (PendingApprovals.IsEmpty)
                return Task.FromResult(Result<string, string>.Success("No pending approval requests."));

            var lines = PendingApprovals.Values.Select(r =>
                $"  [{r.RequestId}] {r.Capability} (risk: {r.RiskLevel}) from device {r.CallerDeviceId[..Math.Min(8, r.CallerDeviceId.Length)]}...");
            var summary = $"Pending Approvals ({PendingApprovals.Count}):\n" + string.Join("\n", lines);

            return Task.FromResult(Result<string, string>.Success(summary));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // openclaw_approval_respond
    // ═════════════════════════════════════════════════════════════════════════════

    public sealed class OpenClawApprovalRespondTool : ITool
    {
        public string Name => "openclaw_approval_respond";
        public string Description => "Approve or deny a pending PC node operation request.";
        public string? JsonSchema => """{"type":"object","properties":{"requestId":{"type":"string","description":"Request ID from the approval list"},"approved":{"type":"boolean","description":"true to approve, false to deny"}},"required":["requestId","approved"]}""";

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedPcNode == null)
                return Task.FromResult(NodeNotRunning());

            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                var requestId = root.GetProperty("requestId").GetString() ?? "";
                var approved = root.GetProperty("approved").GetBoolean();

                if (!PendingApprovals.TryRemove(requestId, out var request))
                    return Task.FromResult(Result<string, string>.Failure(
                        $"No pending request with ID '{requestId}'"));

                if (ApprovalCompletions.TryRemove(requestId, out var tcs))
                    tcs.TrySetResult(approved);

                return Task.FromResult(Result<string, string>.Success(
                    $"Request '{requestId}' ({request.Capability}) {(approved ? "approved" : "denied")}"));
            }
            catch (JsonException)
            {
                return Task.FromResult(Result<string, string>.Failure(
                    "Invalid JSON. Expected: {\"requestId\":\"...\", \"approved\":true}"));
            }
        }
    }
}
