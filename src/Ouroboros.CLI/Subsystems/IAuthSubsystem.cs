// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Application.Auth;
using Ouroboros.Core.Security.Authentication;

/// <summary>
/// Authentication subsystem: manages local device key identity,
/// signing, verification, and principal construction.
/// </summary>
public interface IAuthSubsystem : IAgentSubsystem
{
    /// <summary>Whether the device key has been loaded / generated.</summary>
    bool IsDeviceKeyReady { get; }

    /// <summary>Hex-encoded device fingerprint (SHA-256 of public key).</summary>
    string DeviceId { get; }

    /// <summary>PEM-encoded ECDSA P-256 public key.</summary>
    string PublicKeyPem { get; }

    /// <summary>The underlying device key auth service.</summary>
    DeviceKeyAuthService DeviceAuth { get; }

    /// <summary>Builds an <see cref="AuthenticationPrincipal"/> for this device.</summary>
    AuthenticationPrincipal GetDevicePrincipal();

    /// <summary>Signs a UTF-8 message with the device private key.</summary>
    string Sign(string message);

    /// <summary>Verifies a signature against a UTF-8 message using this device's public key.</summary>
    bool Verify(string message, string signatureBase64Url);
}
