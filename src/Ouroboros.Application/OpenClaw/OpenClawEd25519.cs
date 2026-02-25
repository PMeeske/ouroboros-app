// <copyright file="OpenClawEd25519.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Ouroboros.Application.OpenClaw;

/// <summary>
/// Ed25519 (RFC 8032) operations for OpenClaw device identity signing.
/// Backed by BouncyCastle for verified correctness against RFC 8032 test vectors.
/// </summary>
internal static class OpenClawEd25519
{
    /// <summary>
    /// Generate a new Ed25519 keypair from a cryptographically random 32-byte seed.
    /// Returns (seed, publicKey), each 32 bytes.
    /// </summary>
    public static (byte[] Seed, byte[] PublicKey) GenerateKeyPair()
    {
        byte[] seed = new byte[32];
        RandomNumberGenerator.Fill(seed);
        var pk = GetPublicKey(seed);
        return (seed, pk);
    }

    /// <summary>Derive the 32-byte compressed public key from a 32-byte seed.</summary>
    public static byte[] GetPublicKey(byte[] seed)
    {
        var privateKey = new Ed25519PrivateKeyParameters(seed);
        return privateKey.GeneratePublicKey().GetEncoded();
    }

    /// <summary>
    /// Sign a message using Ed25519 (RFC 8032).
    /// Returns a 64-byte detached signature.
    /// </summary>
    public static byte[] Sign(byte[] message, byte[] seed, byte[] publicKey)
    {
        var privateKey = new Ed25519PrivateKeyParameters(seed);
        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, privateKey);
        signer.BlockUpdate(message, 0, message.Length);
        return signer.GenerateSignature();
    }
}
