// <copyright file="OpenClawEd25519.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Numerics;
using System.Security.Cryptography;

namespace Ouroboros.Application.OpenClaw;

/// <summary>
/// Pure-managed Ed25519 (RFC 8032) for OpenClaw device identity signing.
///
/// Curve: twisted Edwards with a = -1 over GF(2^255 - 19).
/// Equation: -x² + y² = 1 + d·x²·y²
/// All arithmetic is constant-width BigInteger; not constant-time, which is
/// acceptable here because signing is done once at connect time on a local
/// non-adversarial device.
/// </summary>
internal static class OpenClawEd25519
{
    // ── Field constants ───────────────────────────────────────────────────────────

    // p = 2^255 - 19
    private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;

    // L = group order = 2^252 + 27742317777372353535851937790883648493
    private static readonly BigInteger L =
        BigInteger.Pow(2, 252) +
        BigInteger.Parse("27742317777372353535851937790883648493");

    // d = -121665 / 121666 mod p  (curve parameter)
    private static readonly BigInteger D;

    // ── Base point (affine, from RFC 8032 §5.1) ──────────────────────────────────

    private static readonly (BigInteger X, BigInteger Y) BasePoint = (
        BigInteger.Parse("15112221349535807912866137220509078750507884956996801593219959140374088669986"),
        BigInteger.Parse("46316835694926478169428394003475163141307993866256225615783033011972563781"));

    static OpenClawEd25519()
    {
        // d = -121665 * ModInv(121666) mod p
        D = FMod(-121665 * FInv(121666));
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generate a new Ed25519 keypair from a cryptographically random 32-byte seed.
    /// The seed is the private key material — keep it secret.
    /// Returns (seed, publicKey), each 32 bytes.
    /// </summary>
    public static (byte[] Seed, byte[] PublicKey) GenerateKeyPair()
    {
        var seed = new byte[32];
        RandomNumberGenerator.Fill(seed);
        return (seed, GetPublicKey(seed));
    }

    /// <summary>Derive the 32-byte compressed public key from a 32-byte seed.</summary>
    public static byte[] GetPublicKey(byte[] seed)
    {
        var (scalar, _) = ExpandSeed(seed);
        return EncodePoint(ScalarMult(BasePoint, scalar));
    }

    /// <summary>
    /// Sign a message using the Ed25519 deterministic algorithm (RFC 8032 §5.1.6).
    /// Returns a 64-byte signature: R (32 bytes) || S (32 bytes).
    /// </summary>
    public static byte[] Sign(byte[] message, byte[] seed, byte[] publicKey)
    {
        var (a, prefix) = ExpandSeed(seed);

        // r = SHA-512(prefix ‖ M) mod L   — deterministic nonce
        byte[] rHash = SHA512.HashData([.. prefix, .. message]);
        BigInteger r = FModL(DecodeLE(rHash));

        // R = [r]B
        byte[] encodedR = EncodePoint(ScalarMult(BasePoint, r));

        // k = SHA-512(R ‖ A ‖ M) mod L
        byte[] kHash = SHA512.HashData([.. encodedR, .. publicKey, .. message]);
        BigInteger k = FModL(DecodeLE(kHash));

        // S = (r + k·a) mod L
        BigInteger S = FModL(r + k * a);

        // Signature = R ‖ S
        var sig = new byte[64];
        encodedR.CopyTo(sig, 0);
        EncodeLE(S, sig, 32, 32);
        return sig;
    }

    // ── Field arithmetic mod P ────────────────────────────────────────────────────

    private static BigInteger FMod(BigInteger a)
    {
        var r = a % P;
        return r.Sign < 0 ? r + P : r;
    }

    // Modular inverse via Fermat's little theorem (P is prime, so a^(P-2) ≡ a^-1 mod P)
    private static BigInteger FInv(BigInteger a) =>
        BigInteger.ModPow(FMod(a), P - 2, P);

    private static BigInteger FModL(BigInteger a)
    {
        var r = a % L;
        return r.Sign < 0 ? r + L : r;
    }

    // ── Point operations (complete twisted Edwards addition, a = -1) ──────────────
    //
    // Addition law for -x² + y² = 1 + d·x²·y² :
    //   x₃ = (x₁y₂ + y₁x₂) / (1 + d·x₁x₂y₁y₂)
    //   y₃ = (y₁y₂ + x₁x₂) / (1 − d·x₁x₂y₁y₂)
    // Identity element: (0, 1).
    // The law is complete — denominators are never 0 for points on the curve.

    private static (BigInteger X, BigInteger Y) PointAdd(
        (BigInteger X, BigInteger Y) p1,
        (BigInteger X, BigInteger Y) p2)
    {
        BigInteger x1 = p1.X, y1 = p1.Y;
        BigInteger x2 = p2.X, y2 = p2.Y;

        BigInteger dxy = FMod(D * FMod(x1 * x2) % P * FMod(y1 * y2));

        BigInteger x3num = FMod(x1 * y2 + y1 * x2);
        BigInteger x3den = FMod(1 + dxy);
        BigInteger y3num = FMod(y1 * y2 + x1 * x2);
        BigInteger y3den = FMod(1 - dxy);

        return (FMod(x3num * FInv(x3den)), FMod(y3num * FInv(y3den)));
    }

    // Double-and-add scalar multiplication (left-to-right binary method)
    private static (BigInteger X, BigInteger Y) ScalarMult(
        (BigInteger X, BigInteger Y) point, BigInteger scalar)
    {
        (BigInteger X, BigInteger Y) result = (BigInteger.Zero, BigInteger.One); // identity
        (BigInteger X, BigInteger Y) addend = point;

        while (scalar > BigInteger.Zero)
        {
            if (!scalar.IsEven)
                result = PointAdd(result, addend);
            addend = PointAdd(addend, addend);
            scalar >>= 1;
        }

        return result;
    }

    // ── Encoding ─────────────────────────────────────────────────────────────────

    // Encode point: 32-byte little-endian y, with high bit of byte[31] = x parity
    private static byte[] EncodePoint((BigInteger X, BigInteger Y) pt)
    {
        var out32 = new byte[32];
        EncodeLE(pt.Y, out32, 0, 32);
        if (!pt.X.IsEven)
            out32[31] |= 0x80;
        return out32;
    }

    // Interpret byte array as unsigned little-endian BigInteger
    private static BigInteger DecodeLE(byte[] bytes) =>
        new(bytes, isUnsigned: true, isBigEndian: false);

    // Write BigInteger as unsigned little-endian into dest[offset .. offset+length],
    // zero-padding if the value requires fewer bytes.
    private static void EncodeLE(BigInteger value, byte[] dest, int offset, int length)
    {
        byte[] src = value.ToByteArray(isUnsigned: true, isBigEndian: false);
        Array.Clear(dest, offset, length);
        Array.Copy(src, 0, dest, offset, Math.Min(src.Length, length));
    }

    // Expand a 32-byte seed into (clamped scalar a, nonce prefix) per RFC 8032 §5.1.5
    private static (BigInteger Scalar, byte[] Prefix) ExpandSeed(byte[] seed)
    {
        byte[] h = SHA512.HashData(seed);
        byte[] aBytes = (byte[])h[0..32].Clone();

        // RFC 8032 clamping
        aBytes[0] &= 248;   // clear bottom 3 bits
        aBytes[31] &= 127;  // clear top bit
        aBytes[31] |= 64;   // set second-to-top bit

        return (DecodeLE(aBytes), h[32..64]);
    }
}
