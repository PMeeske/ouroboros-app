// Copyright (c) Ouroboros. All rights reserved.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ouroboros.Core.Security.Authentication;

namespace Ouroboros.Application.Auth;

/// <summary>
/// Local device key authentication using an ECDSA P-256 keypair persisted to disk.
///
/// On first run, generates a keypair and stores it at
/// <c>%APPDATA%/Ouroboros/device_auth.json</c>.  Subsequent runs load the
/// existing key so the device identity stays stable across restarts.
///
/// Implements <see cref="IAuthenticationProvider"/> — the device "authenticates"
/// by signing a challenge with its private key.  The resulting principal carries
/// the device fingerprint and hostname as claims.
/// </summary>
public sealed class DeviceKeyAuthService : IAuthenticationProvider, IDisposable
{
    private const string AppSubDir = "Ouroboros";
    private const string FileName = "device_auth.json";

    private ECDsa? _key;
    private string _deviceId = string.Empty;
    private string _publicKeyPem = string.Empty;
    private bool _initialized;

    /// <summary>Hex-encoded SHA-256 of the DER public key (64 chars).</summary>
    public string DeviceId => _deviceId;

    /// <summary>PEM-encoded public key for sharing / verification.</summary>
    public string PublicKeyPem => _publicKeyPem;

    /// <summary>Whether the service has loaded or created a keypair.</summary>
    public bool IsInitialized => _initialized;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads existing keypair from disk or generates a fresh one.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        var path = StoragePath();

        if (File.Exists(path))
        {
            try
            {
                await LoadAsync(path, ct);
                _initialized = true;
                return;
            }
            catch (Exception) { /* Corrupted — regenerate */ }
        }

        GenerateNewKey();
        await PersistAsync(path, ct);
        _initialized = true;
    }

    // ── Signing / Verification ────────────────────────────────────────────────

    /// <summary>
    /// Signs arbitrary data with the device private key (SHA-256 digest).
    /// Returns a base64url-encoded signature.
    /// </summary>
    public string Sign(byte[] data)
    {
        EnsureInitialized();
        var sig = _key!.SignData(data, HashAlgorithmName.SHA256);
        return ToBase64Url(sig);
    }

    /// <summary>
    /// Signs a UTF-8 string with the device private key.
    /// </summary>
    public string Sign(string message) => Sign(Encoding.UTF8.GetBytes(message));

    /// <summary>
    /// Verifies a signature against arbitrary data using this device's public key.
    /// </summary>
    public bool Verify(byte[] data, string signatureBase64Url)
    {
        EnsureInitialized();
        var sig = FromBase64Url(signatureBase64Url);
        return _key!.VerifyData(data, sig, HashAlgorithmName.SHA256);
    }

    /// <summary>
    /// Verifies a signature produced by a specific public key (PEM).
    /// Useful for verifying that data came from another known device.
    /// </summary>
    public static bool VerifyWith(byte[] data, string signatureBase64Url, string publicKeyPem)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(publicKeyPem);
        var sig = FromBase64Url(signatureBase64Url);
        return ecdsa.VerifyData(data, sig, HashAlgorithmName.SHA256);
    }

    // ── IAuthenticationProvider ────────────────────────────────────────────────

    /// <summary>
    /// Authenticates the local device.  The <paramref name="username"/> is ignored;
    /// the <paramref name="password"/> is treated as a challenge string that is
    /// signed with the device key.  The resulting token IS the signature so the
    /// caller can later verify it.
    /// </summary>
    public Task<AuthenticationResult> AuthenticateAsync(
        string username, string password, CancellationToken ct = default)
    {
        if (!_initialized)
            return Task.FromResult(AuthenticationResult.Failure("Device key not initialized"));

        var challenge = password; // caller-provided challenge
        var signature = Sign(challenge);

        var principal = BuildPrincipal();
        return Task.FromResult(AuthenticationResult.Success(principal, signature));
    }

    /// <summary>
    /// Validates a token (signature) against the stored challenge claim.
    /// For device-key auth the token is a signature of a known challenge.
    /// </summary>
    public Task<AuthenticationResult> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        if (!_initialized)
            return Task.FromResult(AuthenticationResult.Failure("Device key not initialized"));

        // Token format: base64url(signature)|challenge
        var parts = token.Split('|', 2);
        if (parts.Length != 2)
            return Task.FromResult(AuthenticationResult.Failure("Invalid token format — expected signature|challenge"));

        var sig = parts[0];
        var challenge = parts[1];

        if (!Verify(Encoding.UTF8.GetBytes(challenge), sig))
            return Task.FromResult(AuthenticationResult.Failure("Signature verification failed"));

        return Task.FromResult(AuthenticationResult.Success(BuildPrincipal(), token));
    }

    public Task<AuthenticationResult> RefreshTokenAsync(string token, CancellationToken ct = default)
        => Task.FromResult(AuthenticationResult.Failure("Device key auth does not support token refresh"));

    public Task<bool> RevokeTokenAsync(string token, CancellationToken ct = default)
        => Task.FromResult(false); // device keys are not revocable via this path

    // ── Principal ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="AuthenticationPrincipal"/> representing this device.
    /// </summary>
    public AuthenticationPrincipal BuildPrincipal()
    {
        EnsureInitialized();
        return new AuthenticationPrincipal
        {
            Id = _deviceId,
            Name = Environment.MachineName,
            Roles = ["device", "local-agent"],
            Claims = new Dictionary<string, string>
            {
                ["device_id"] = _deviceId,
                ["hostname"] = Environment.MachineName,
                ["os"] = Environment.OSVersion.ToString(),
                ["user"] = Environment.UserName,
                ["auth_method"] = "device_key_ecdsa_p256",
            },
            ExpiresAt = DateTime.UtcNow.AddYears(10), // device keys don't expire
        };
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private void GenerateNewKey()
    {
        _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        _publicKeyPem = _key.ExportSubjectPublicKeyInfoPem();
        _deviceId = ComputeDeviceId(_key);
    }

    private async Task LoadAsync(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        var dto = await JsonSerializer.DeserializeAsync<DeviceKeyDto>(fs, cancellationToken: ct)
                  ?? throw new InvalidDataException("Null device key storage");

        _key = ECDsa.Create();
        _key.ImportECPrivateKey(Convert.FromBase64String(dto.PrivateKey), out _);
        _publicKeyPem = _key.ExportSubjectPublicKeyInfoPem();
        _deviceId = ComputeDeviceId(_key);
    }

    private async Task PersistAsync(string path, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var dto = new DeviceKeyDto
        {
            PrivateKey = Convert.ToBase64String(_key!.ExportECPrivateKey()),
            DeviceId = _deviceId,
            CreatedAt = DateTime.UtcNow.ToString("O"),
            Hostname = Environment.MachineName,
        };
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, dto, cancellationToken: ct);
    }

    private static string ComputeDeviceId(ECDsa key)
    {
        var pub = key.ExportSubjectPublicKeyInfo();
        return Convert.ToHexString(SHA256.HashData(pub)).ToLowerInvariant();
    }

    private void EnsureInitialized()
    {
        if (!_initialized || _key == null)
            throw new InvalidOperationException("DeviceKeyAuthService not initialized — call InitializeAsync first");
    }

    private static string StoragePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppSubDir,
            FileName);

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }

    public void Dispose() => _key?.Dispose();

    // ── Storage DTO ───────────────────────────────────────────────────────────

    private sealed class DeviceKeyDto
    {
        [JsonPropertyName("privateKey")]
        public string PrivateKey { get; set; } = string.Empty;

        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("hostname")]
        public string Hostname { get; set; } = string.Empty;
    }
}
