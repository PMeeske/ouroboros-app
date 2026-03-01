// <copyright file="OpenClawDeviceIdentity.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.Application.OpenClaw;

/// <summary>
/// Manages the stable Ed25519 device identity used for OpenClaw Gateway pairing.
///
/// On first run an Ed25519 keypair is generated and persisted to
/// <c>%APPDATA%/Ouroboros/openclaw_device.json</c>.  Subsequent runs load the
/// existing keypair so the device ID stays stable across restarts.
///
/// After a successful pairing the gateway returns a <c>deviceToken</c> that is
/// also persisted and sent on future connects, avoiding a full re-pairing.
/// </summary>
public sealed class OpenClawDeviceIdentity
{
    private const string AppSubDir = "Ouroboros";
    private const string FileName = "openclaw_device.json";

    private readonly byte[] _seed;
    private readonly byte[] _publicKey;

    /// <summary>
    /// Stable device identifier: SHA-256 of the raw 32-byte Ed25519 public key,
    /// hex-encoded lowercase (64 chars, no prefix).
    /// </summary>
    public string DeviceId { get; }

    /// <summary>Base64Url-encoded 32-byte Ed25519 public key (no padding).</summary>
    public string PublicKeyBase64Url => ToBase64Url(_publicKey);

    /// <summary>
    /// Device token received from the gateway after the first successful pairing.
    /// Null until the gateway returns one.
    /// </summary>
    public string? DeviceToken { get; private set; }

    private OpenClawDeviceIdentity(byte[] seed, byte[] publicKey, string deviceId, string? deviceToken)
    {
        _seed = seed;
        _publicKey = publicKey;
        DeviceId = deviceId;
        DeviceToken = deviceToken;
    }

    // ── Factory ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Load the device identity from disk, generating a fresh keypair if no
    /// persisted identity exists (or if the stored file is unreadable).
    /// </summary>
    public static async Task<OpenClawDeviceIdentity> LoadOrCreateAsync(CancellationToken ct = default)
    {
        var path = StoragePath();

        if (File.Exists(path))
        {
            try { return await LoadAsync(path, ct); }
            catch (System.Text.Json.JsonException) { /* Corrupted — regenerate below */ }
            catch (FormatException) { /* Invalid Base64 seed — regenerate below */ }
            catch (IOException) { /* Unreadable file — regenerate below */ }
        }

        var fresh = Create();
        await fresh.PersistAsync(path, ct);
        return fresh;
    }

    // ── Signing ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Build and sign the OpenClaw v2 handshake payload.
    ///
    /// Payload format (pipe-separated, UTF-8):
    ///   <c>v2|{deviceId}|{clientId}|{clientMode}|{role}|{scopesCsv}|{signedAtMs}|{tokenOrEmpty}|{nonce}</c>
    ///
    /// Returns the values needed for <c>connect.params.device</c>.
    /// </summary>
    public (string Signature, long SignedAt, string Nonce) SignHandshake(
        string nonce,
        string clientId,
        string clientMode,
        string role,
        string scopesCsv,
        string tokenOrEmpty)
    {
        long signedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string payload = $"v2|{DeviceId}|{clientId}|{clientMode}|{role}|{scopesCsv}|{signedAt}|{tokenOrEmpty}|{nonce}";
        System.Diagnostics.Debug.WriteLine($"[OpenClaw] Signing payload: {payload}");
        byte[] data = Encoding.UTF8.GetBytes(payload);
        byte[] rawSig = OpenClawEd25519.Sign(data, _seed, _publicKey);
        return (ToBase64Url(rawSig), signedAt, nonce);
    }

    // ── Token persistence ─────────────────────────────────────────────────────────

    /// <summary>
    /// Deletes the persisted device identity and creates a fresh Ed25519 keypair.
    /// Called automatically by the gateway client when the server rejects the
    /// device signature (e.g. after key rotation or file corruption).
    /// </summary>
    public static async Task<OpenClawDeviceIdentity> RegenerateAsync(CancellationToken ct = default)
    {
        var path = StoragePath();
        try { File.Delete(path); } catch (IOException) { /* best-effort */ }
        var fresh = Create();
        await fresh.PersistAsync(path, ct);
        return fresh;
    }

    /// <summary>
    /// Store the device token returned by the gateway in memory and on disk.
    /// Called after a successful hello-ok that includes <c>auth.deviceToken</c>.
    /// </summary>
    public async Task SaveDeviceTokenAsync(string token, CancellationToken ct = default)
    {
        DeviceToken = token;
        try { await PersistAsync(StoragePath(), ct); }
        catch (IOException) { /* Non-fatal — token will be re-acquired on next connect */ }
    }

    // ── Internals ─────────────────────────────────────────────────────────────────

    private static OpenClawDeviceIdentity Create()
    {
        var (seed, pub) = OpenClawEd25519.GenerateKeyPair();
        var id = ComputeDeviceId(pub);
        return new OpenClawDeviceIdentity(seed, pub, id, null);
    }

    private static async Task<OpenClawDeviceIdentity> LoadAsync(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        var dto = await JsonSerializer.DeserializeAsync<StorageDto>(fs, cancellationToken: ct)
                  ?? throw new InvalidDataException("Null device storage");

        byte[] seed = Convert.FromBase64String(dto.Seed);
        byte[] pub = OpenClawEd25519.GetPublicKey(seed);
        string id = ComputeDeviceId(pub);
        return new OpenClawDeviceIdentity(seed, pub, id, dto.DeviceToken);
    }

    private async Task PersistAsync(string path, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var dto = new StorageDto
        {
            Seed = Convert.ToBase64String(_seed),
            DeviceId = DeviceId,
            DeviceToken = DeviceToken,
            CreatedAt = DateTime.UtcNow.ToString("O"),
        };
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, dto, cancellationToken: ct);
    }

    /// <summary>SHA-256 of the raw 32-byte public key, hex-encoded lowercase.</summary>
    private static string ComputeDeviceId(byte[] publicKey) =>
        Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string StoragePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppSubDir,
            FileName);

    // ── Storage DTO ───────────────────────────────────────────────────────────────

    private sealed class StorageDto
    {
        [JsonPropertyName("seed")]
        public string Seed { get; set; } = string.Empty;

        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonPropertyName("deviceToken")]
        public string? DeviceToken { get; set; }

        [JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = string.Empty;
    }

}
