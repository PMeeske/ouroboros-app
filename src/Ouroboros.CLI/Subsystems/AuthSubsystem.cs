// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Application.Auth;
using Ouroboros.Core.Security.Authentication;

/// <summary>
/// Auth subsystem: initializes and manages the local device key identity.
/// On first run generates an ECDSA P-256 keypair persisted to
/// <c>%APPDATA%/Ouroboros/device_auth.json</c>.
/// </summary>
public sealed class AuthSubsystem : IAuthSubsystem
{
    public string Name => "Auth";
    public bool IsInitialized { get; private set; }

    private readonly DeviceKeyAuthService _deviceAuth = new();

    /// <inheritdoc/>
    public bool IsDeviceKeyReady => _deviceAuth.IsInitialized;

    /// <inheritdoc/>
    public string DeviceId => _deviceAuth.DeviceId;

    /// <inheritdoc/>
    public string PublicKeyPem => _deviceAuth.PublicKeyPem;

    /// <inheritdoc/>
    public DeviceKeyAuthService DeviceAuth => _deviceAuth;

    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        try
        {
            await _deviceAuth.InitializeAsync();
            IsInitialized = true;
            ctx.Output.RecordInit("Auth", true,
                $"device {_deviceAuth.DeviceId[..12]}... ({Environment.MachineName})");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            IsInitialized = true; // non-fatal — agent can run without auth
            ctx.Output.RecordInit("Auth", false, $"device key failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public AuthenticationPrincipal GetDevicePrincipal() => _deviceAuth.BuildPrincipal();

    /// <inheritdoc/>
    public string Sign(string message) => _deviceAuth.Sign(message);

    /// <inheritdoc/>
    public bool Verify(string message, string signatureBase64Url) =>
        _deviceAuth.Verify(System.Text.Encoding.UTF8.GetBytes(message), signatureBase64Url);

    public ValueTask DisposeAsync()
    {
        _deviceAuth.Dispose();
        return ValueTask.CompletedTask;
    }
}
