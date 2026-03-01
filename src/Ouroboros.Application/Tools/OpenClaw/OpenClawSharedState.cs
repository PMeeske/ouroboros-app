// <copyright file="OpenClawSharedState.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Shared static state and helper methods used by all OpenClaw tool implementations.
/// </summary>
internal static class OpenClawSharedState
{
    private const string DefaultGateway = Configuration.DefaultEndpoints.OpenClawGateway;

    /// <summary>Shared gateway client, set during init.</summary>
    public static OpenClawGatewayClient? SharedClient { get; set; }

    /// <summary>Shared security policy for message/node validation.</summary>
    public static OpenClawSecurityPolicy? SharedPolicy { get; set; }

    /// <summary>
    /// Connects to the OpenClaw Gateway, sets up security, and populates
    /// <see cref="SharedClient"/> / <see cref="SharedPolicy"/>.
    /// CLI option values take precedence; env vars are used as fallback.
    /// </summary>
    /// <param name="gatewayUrl">Explicit gateway URL (from --openclaw-gateway), or null for env/default.</param>
    /// <param name="token">Explicit token (from --openclaw-token), or null for env.</param>
    /// <returns>The resolved gateway URL on success, or null if connection failed.</returns>
    public static async Task<string?> ConnectGatewayAsync(
        string? gatewayUrl = null,
        string? token = null)
    {
        string resolvedGateway = gatewayUrl
            ?? Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY")
            ?? DefaultGateway;
        string? resolvedToken = token
            ?? Environment.GetEnvironmentVariable("OPENCLAW_TOKEN");

        var deviceIdentity = await OpenClawDeviceIdentity.LoadOrCreateAsync();
        var client = new OpenClawGatewayClient(deviceIdentity);
        try
        {
            await client.ConnectAsync(new Uri(resolvedGateway), resolvedToken, CancellationToken.None);
        }
        catch (OpenClawException ex) when (
            ex.Message.Contains("device signature", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("device identity", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("pairing required", StringComparison.OrdinalIgnoreCase))
        {
            // Auto-pairing was attempted inside SendConnectHandshakeAsync via the openclaw CLI.
            // Dispose and retry ONCE with the same device identity — it should now be approved.
            await client.DisposeAsync();
            client = new OpenClawGatewayClient(deviceIdentity);
            await client.ConnectAsync(new Uri(resolvedGateway), resolvedToken, CancellationToken.None);
        }
        SharedClient = client;

        var auditLog = new OpenClawAuditLog();
        var securityConfig = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development"
            ? OpenClawSecurityConfig.CreateDevelopment()
            : OpenClawSecurityConfig.CreateDefault();
        SharedPolicy = new OpenClawSecurityPolicy(securityConfig, auditLog);

        return resolvedGateway;
    }

    /// <summary>Returns a failure result indicating the gateway is not connected.</summary>
    public static Result<string, string> NotConnected() =>
        Result<string, string>.Failure("OpenClaw Gateway is not connected. Ensure the gateway is running and --enable-openclaw is set.");

    /// <summary>Returns a failure result indicating the security policy is not initialized.</summary>
    public static Result<string, string> PolicyNotInitialized() =>
        Result<string, string>.Failure("OpenClaw security policy not initialized. Write operations are blocked until the policy is configured.");
}
