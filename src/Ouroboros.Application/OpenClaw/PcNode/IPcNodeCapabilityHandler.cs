// <copyright file="IPcNodeCapabilityHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Ouroboros.Application.OpenClaw.PcNode;

/// <summary>
/// Contract for a PC node capability that can be invoked remotely via OpenClaw.
/// Each handler exposes a single named capability (e.g. "system.info", "screen.capture")
/// and declares its risk level for the approval workflow.
/// </summary>
public interface IPcNodeCapabilityHandler
{
    /// <summary>Capability name as advertised to the gateway (e.g. "system.info").</summary>
    string CapabilityName { get; }

    /// <summary>Human-readable description of what this capability does.</summary>
    string Description { get; }

    /// <summary>Risk classification used by the approval workflow.</summary>
    PcNodeRiskLevel RiskLevel { get; }

    /// <summary>Optional JSON Schema for the parameters this capability accepts.</summary>
    string? ParameterSchema { get; }

    /// <summary>
    /// Whether this capability always requires interactive user approval before execution,
    /// regardless of the <see cref="PcNodeSecurityConfig.ApprovalThreshold"/>.
    /// </summary>
    bool RequiresApproval { get; }

    /// <summary>Executes the capability and returns a result.</summary>
    Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters,
        PcNodeExecutionContext context,
        CancellationToken ct);
}

/// <summary>
/// Risk classification for PC node capabilities. Higher levels trigger stricter
/// security controls and approval requirements.
/// </summary>
public enum PcNodeRiskLevel
{
    /// <summary>Read-only system info, notifications. No approval by default.</summary>
    Low,

    /// <summary>Screen capture, file read, process list. Approval configurable.</summary>
    Medium,

    /// <summary>File write/delete, process kill, app launch. Approval required by default.</summary>
    High,

    /// <summary>Shell commands. Always requires approval, disabled by default.</summary>
    Critical,
}

/// <summary>
/// Result returned by a capability handler after execution.
/// </summary>
/// <param name="Success">Whether the operation completed successfully.</param>
/// <param name="Data">Structured JSON result data (null on failure).</param>
/// <param name="Error">Error message (null on success).</param>
/// <param name="Base64Payload">Optional binary payload encoded as base64 (screenshots, recordings).</param>
public sealed record PcNodeResult(
    bool Success,
    JsonElement? Data,
    string? Error,
    string? Base64Payload)
{
    /// <summary>Creates a success result with JSON data.</summary>
    public static PcNodeResult Ok(JsonElement data) => new(true, data, null, null);

    /// <summary>Creates a success result with a plain text message.</summary>
    public static PcNodeResult Ok(string message)
    {
        var json = JsonSerializer.SerializeToElement(new { message });
        return new(true, json, null, null);
    }

    /// <summary>Creates a success result with a binary payload (e.g. screenshot).</summary>
    public static PcNodeResult OkWithPayload(string base64, string? description = null)
    {
        var data = description != null
            ? JsonSerializer.SerializeToElement(new { description })
            : (JsonElement?)null;
        return new(true, data, null, base64);
    }

    /// <summary>Creates a failure result.</summary>
    public static PcNodeResult Fail(string error) => new(false, null, error, null);
}

/// <summary>
/// Contextual information passed to capability handlers during execution.
/// </summary>
/// <param name="RequestId">Unique identifier for this invocation request.</param>
/// <param name="CallerDeviceId">Device ID of the remote caller.</param>
/// <param name="RequestTime">UTC timestamp when the request was received.</param>
/// <param name="AuditLog">Shared audit log for recording the operation.</param>
public sealed record PcNodeExecutionContext(
    string RequestId,
    string CallerDeviceId,
    DateTime RequestTime,
    OpenClawAuditLog AuditLog);

/// <summary>
/// Descriptor advertised to the gateway during the node handshake,
/// telling operators what this node can do.
/// </summary>
/// <param name="Name">Capability name (e.g. "system.info").</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="ParameterSchema">Optional JSON Schema for parameters.</param>
public sealed record CapabilityDescriptor(
    string Name,
    string Description,
    string? ParameterSchema);
